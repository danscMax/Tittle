using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tittle.Core.Text;

/// <summary>Conservative language guesser for fenced code blocks that carry NO info string. The guess
/// is written back into the fence (```json), which the viewer's TextMate highlighter then colours.
/// Heuristics only — when nothing matches confidently it returns <c>null</c> and the block stays plain.
/// Emitted names are canonical TextMate ids/aliases (json, xml, python, …). ponytail: a known-ceiling
/// heuristic, not a real classifier; swap in a detector library if it starts mis-guessing.</summary>
public static partial class CodeLanguageGuess
{
    public static string? Guess(IReadOnlyList<string> body)
    {
        if (body is null || body.Count == 0)
            return null;

        var text = string.Join("\n", body);
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
            return null;

        // Markup first — a leading '<' is unambiguous vs the brace/keyword languages below.
        if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || XmlTag().IsMatch(trimmed))
            return "xml";

        // JSON: a brace/bracket root with at least one "key": pair (distinguishes from a C-like block).
        if ((trimmed[0] == '{' || trimmed[0] == '[') && JsonKey().IsMatch(text))
            return "json";

        // Shell: a shebang, or prompt-style `$ ` lines.
        var firstLine = body.FirstOrDefault(l => l.Trim().Length > 0)?.TrimStart() ?? "";
        if (firstLine.StartsWith("#!") && (firstLine.Contains("sh") || firstLine.Contains("bash"))
            || body.Any(l => l.StartsWith("$ ")))
            return "bash";

        // SQL: a leading statement keyword paired with its usual companion.
        if (SqlStatement().IsMatch(text))
            return "sql";

        // Python: def/class headers, imports, or print(.
        if (PythonSignal().IsMatch(text))
            return "python";

        // C#: namespaces, using directives, access-modified members, Console.
        if (CSharpSignal().IsMatch(text))
            return "csharp";

        // JavaScript: function/const/let/var, arrow functions, console.
        if (JsSignal().IsMatch(text))
            return "javascript";

        return null;
    }

    [GeneratedRegex(@"^<[a-zA-Z][\w:-]*(\s|>|/)")]
    private static partial Regex XmlTag();

    [GeneratedRegex(@"""[^""\n]*""\s*:")]
    private static partial Regex JsonKey();

    [GeneratedRegex(@"\b(SELECT\b[\s\S]*\bFROM|INSERT\s+INTO|UPDATE\b[\s\S]*\bSET|DELETE\s+FROM|CREATE\s+(TABLE|VIEW|INDEX))\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SqlStatement();

    [GeneratedRegex(@"(^|\n)\s*(def\s+\w+\s*\(|class\s+\w+\s*[:\(]|import\s+\w|from\s+\w+\s+import\b|print\s*\()")]
    private static partial Regex PythonSignal();

    [GeneratedRegex(@"(^|\n)\s*(using\s+[\w.]+;|namespace\s+[\w.]+|(public|private|internal|protected)\s+(static\s+)?(class|void|int|string|bool|record)\b)|Console\.")]
    private static partial Regex CSharpSignal();

    [GeneratedRegex(@"(^|\n)\s*(function\s+\w*\s*\(|const\s+\w+\s*=|let\s+\w+\s*=|var\s+\w+\s*=)|=>|console\.(log|error|warn)\b")]
    private static partial Regex JsSignal();
}
