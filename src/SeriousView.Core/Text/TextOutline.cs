using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>
/// Plain-text heading detection (ported from the original viewer's text-outline heuristics):
/// decorated (`==== T ====`), hash (`# T`), sigil (`§/▶/■/●/◆ T`), chapter words
/// (`Глава N` / `Chapter N` / `ЧАСТЬ` / `Слайд` / `Лекция` / `Section`), and conservative
/// ALL-CAPS lines (2–10 words, no lowercase, no trailing punctuation). Emits the shared
/// <see cref="HeadingOutline"/> shape, so the outline panel / breadcrumbs / scroll-spy work
/// for .txt and .log files unchanged. Capped at 500 headings.
/// </summary>
public static partial class TextOutline
{
    private const int MaxHeadings = 500;

    public static IReadOnlyList<HeadingOutline> Parse(string? text)
    {
        var result = new List<HeadingOutline>();
        if (string.IsNullOrEmpty(text))
            return result;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length && result.Count < MaxHeadings; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.Length > 200)
                continue;

            string? title = null;
            var level = 0;

            if (Decorated().Match(line) is { Success: true } dec)
            {
                title = dec.Groups["t"].Value.Trim();
                level = 1;
            }
            else if (Hash().Match(line) is { Success: true } hash)
            {
                title = hash.Groups["t"].Value.Trim();
                level = Math.Min(4, hash.Groups["h"].Value.Length);
            }
            else if (Sigil().Match(line) is { Success: true } sig)
            {
                title = sig.Groups["t"].Value.Trim();
                level = 3;
            }
            else if (Chapter().IsMatch(line))
            {
                title = line;
                level = 2;
            }
            else if (IsAllCapsHeading(line))
            {
                title = line;
                level = 2;
            }

            if (!string.IsNullOrEmpty(title))
                result.Add(new HeadingOutline(title, level, i + 1, result.Count));
        }

        return result;
    }

    /// <summary>2–10 words, no lowercase letter anywhere, no trailing sentence punctuation.</summary>
    private static bool IsAllCapsHeading(string line)
    {
        if (line.Length < 4 || ".?!,:;".Contains(line[^1]))
            return false;
        if (line.Any(char.IsLower))
            return false;
        if (!line.Any(char.IsUpper))
            return false;

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return words is >= 2 and <= 10;
    }

    [GeneratedRegex(@"^[=\-]{3,}\s*(?<t>.+?)\s*[=\-]{3,}$")]
    private static partial Regex Decorated();

    [GeneratedRegex(@"^(?<h>#{1,6})\s+(?<t>.+)$")]
    private static partial Regex Hash();

    [GeneratedRegex(@"^[§▶■●◆]\s+(?<t>.+)$")]
    private static partial Regex Sigil();

    [GeneratedRegex(@"^(?:Глава|ЧАСТЬ|Часть|Слайд|Лекция|Раздел|Chapter|Section|SLIDE|Slide|Part)\s+\d+", RegexOptions.IgnoreCase)]
    private static partial Regex Chapter();
}
