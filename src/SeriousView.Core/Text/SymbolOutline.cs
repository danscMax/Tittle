using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>
/// Code-symbol outline for non-markdown source files (ported from the original viewer's
/// per-language regex heuristics): classes/functions/methods become the same
/// <see cref="HeadingOutline"/> shape the markdown outline uses, so the TOC panel,
/// breadcrumbs, active-heading tracking and navigation all work for code unchanged.
/// Heuristic by design — regexes, not parsers; level comes from leading indentation divided
/// by the file's smallest non-zero indent. Capped at 20 000 lines; lines longer than 500
/// chars are skipped (minified blobs).
/// </summary>
public static partial class SymbolOutline
{
    private const int MaxLines = 20_000;
    private const int MaxLineLength = 500;
    private const int MaxLevel = 6;

    public static bool Supports(string? extension)
        => extension is not null && Patterns.ContainsKey(extension.ToLowerInvariant());

    public static IReadOnlyList<HeadingOutline> Parse(string? text, string? extension)
    {
        var result = new List<HeadingOutline>();
        if (string.IsNullOrEmpty(text) || extension is null
            || !Patterns.TryGetValue(extension.ToLowerInvariant(), out var patterns))
            return result;

        var lines = LineEndings.NormalizeToLf(text).Split('\n');
        var count = Math.Min(lines.Length, MaxLines);

        // First sweep: collect raw matches with their indentation.
        var raw = new List<(string Name, int Indent, int Line)>();
        for (var i = 0; i < count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line.Length > MaxLineLength)
                continue;

            foreach (var pattern in patterns)
            {
                var m = pattern.Match(line);
                if (!m.Success)
                    continue;
                raw.Add((m.Groups["name"].Value, m.Groups["indent"].Value.Length, i + 1));
                break;
            }
        }

        if (raw.Count == 0)
            return result;

        // Indent step = the smallest non-zero indent seen among symbols (defaults to 4).
        var step = raw.Where(s => s.Indent > 0).Select(s => s.Indent).DefaultIfEmpty(4).Min();
        foreach (var (name, indent, line) in raw)
        {
            var level = Math.Min(MaxLevel, indent / Math.Max(1, step) + 1);
            result.Add(new HeadingOutline(name, level, line, result.Count));
        }

        return result;
    }

    // One list of compiled patterns per extension; every regex exposes (?<indent>) + (?<name>).
    private static readonly Dictionary<string, Regex[]> Patterns = Build();

    private static Dictionary<string, Regex[]> Build()
    {
        var classLike = ClassLike();
        var cFamilyMethod = CFamilyMethod();
        var jsFunction = JsFunction();
        var jsArrow = JsArrow();
        var pyDef = PyDef();
        var pyClass = PyClass();
        var goFunc = GoFunc();
        var rsFn = RsFn();
        var psFunction = PsFunction();
        var rubyDef = RubyDef();
        var phpFunction = PhpFunction();

        var cs = new[] { classLike, cFamilyMethod };
        var js = new[] { classLike, jsFunction, jsArrow };
        var py = new[] { pyClass, pyDef };
        var cpp = new[] { classLike, cFamilyMethod };

        var map = new Dictionary<string, Regex[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".cs"] = cs,
            [".java"] = cs,
            [".kt"] = cs,
            [".swift"] = cs,
            [".js"] = js,
            [".jsx"] = js,
            [".ts"] = js,
            [".tsx"] = js,
            [".mjs"] = js,
            [".py"] = py,
            [".go"] = new[] { classLike, goFunc },
            [".rs"] = new[] { classLike, rsFn },
            [".c"] = cpp,
            [".h"] = cpp,
            [".cpp"] = cpp,
            [".hpp"] = cpp,
            [".ps1"] = new[] { psFunction },
            [".psm1"] = new[] { psFunction },
            [".rb"] = new[] { rubyDef },
            [".php"] = new[] { classLike, phpFunction },
        };
        return map;
    }

    [GeneratedRegex(@"^(?<indent>\s*)(?:export\s+)?(?:default\s+)?(?:public\s+|private\s+|protected\s+|internal\s+|abstract\s+|sealed\s+|partial\s+|static\s+|final\s+|open\s+|pub\s+)*(?:class|interface|struct|enum|record|trait|object)\s+(?<name>[A-Za-z_]\w*)")]
    private static partial Regex ClassLike();

    // C-family method/ctor: an access modifier followed by a signature ending in "name(".
    [GeneratedRegex(@"^(?<indent>\s*)(?:public|private|protected|internal)[\w\s<>,\[\]\?\.]*?\s(?<name>[A-Za-z_]\w*)\s*\(")]
    private static partial Regex CFamilyMethod();

    [GeneratedRegex(@"^(?<indent>\s*)(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s*\*?\s*(?<name>[A-Za-z_$][\w$]*)")]
    private static partial Regex JsFunction();

    [GeneratedRegex(@"^(?<indent>\s*)(?:export\s+)?(?:const|let|var)\s+(?<name>[A-Za-z_$][\w$]*)\s*=\s*(?:async\s*)?\(")]
    private static partial Regex JsArrow();

    [GeneratedRegex(@"^(?<indent>\s*)(?:async\s+)?def\s+(?<name>\w+)")]
    private static partial Regex PyDef();

    [GeneratedRegex(@"^(?<indent>\s*)class\s+(?<name>\w+)")]
    private static partial Regex PyClass();

    [GeneratedRegex(@"^(?<indent>)func\s+(?:\([^)]*\)\s*)?(?<name>\w+)")]
    private static partial Regex GoFunc();

    [GeneratedRegex(@"^(?<indent>\s*)(?:pub(?:\([^)]*\))?\s+)?(?:async\s+)?(?:unsafe\s+)?fn\s+(?<name>\w+)")]
    private static partial Regex RsFn();

    [GeneratedRegex(@"^(?<indent>\s*)function\s+(?<name>[\w-]+)", RegexOptions.IgnoreCase)]
    private static partial Regex PsFunction();

    [GeneratedRegex(@"^(?<indent>\s*)(?:def|class|module)\s+(?<name>[\w.]+)")]
    private static partial Regex RubyDef();

    [GeneratedRegex(@"^(?<indent>\s*)(?:public\s+|private\s+|protected\s+|static\s+)*function\s+(?<name>\w+)")]
    private static partial Regex PhpFunction();
}
