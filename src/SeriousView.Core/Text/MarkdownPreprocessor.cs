using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>Pure markdown text passes that bridge GitHub-flavoured syntax the renderer
/// (Markdown.Avalonia) does not handle natively into forms it does, UI-free and testable:
/// <list type="bullet">
/// <item>GitHub alerts (<c>&gt; [!NOTE]</c> …) → <c>::: admonition-&lt;type&gt;</c> container
///   blocks, rendered as themed callouts by <c>AdmonitionBlockHandler</c>.</item>
/// <item>GFM task lists (<c>- [x]</c> / <c>- [ ]</c>) → checkbox glyphs (the engine renders
///   the markers literally otherwise).</item>
/// </list></summary>
public static partial class MarkdownPreprocessor
{
    /// <summary>Apply all markdown-normalising passes. Returns the input unchanged when there
    /// is nothing to transform (plain markdown round-trips, modulo CRLF → LF normalisation).</summary>
    public static string Transform(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown ?? string.Empty;

        var lines = new List<string>(
            markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));

        lines = ConvertAdmonitions(lines);
        ConvertTaskListsInPlace(lines);

        return string.Join("\n", lines);
    }

    // A GitHub alert opens with the marker alone on a quoted line; subsequent quoted
    // lines are its body, ending at the first non-quoted line.
    private static List<string> ConvertAdmonitions(List<string> lines)
    {
        var result = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var start = AlertStart().Match(lines[i]);
            if (!start.Success)
            {
                result.Add(lines[i]);
                continue;
            }

            var type = start.Groups[1].Value.ToLowerInvariant();
            var body = new List<string>();
            var j = i + 1;
            for (; j < lines.Count; j++)
            {
                var quoted = QuoteLine().Match(lines[j]);
                if (!quoted.Success)
                    break;
                body.Add(quoted.Groups[1].Value);
            }

            // Blank lines around the container so the engine parses it as its own block.
            // The block name is the bare type ("note", "tip", …) — the container parser
            // truncates names at a hyphen, so "admonition-note" would lose its type.
            result.Add(string.Empty);
            result.Add($"::: {type}");
            result.AddRange(body);
            result.Add(":::");
            result.Add(string.Empty);
            i = j - 1; // the for-loop's i++ resumes at the first non-quoted line
        }
        return result;
    }

    private static void ConvertTaskListsInPlace(List<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var item = TaskItem().Match(lines[i]);
            if (!item.Success)
                continue;

            var glyph = item.Groups[2].Value is "x" or "X" ? "☑" : "☐";
            lines[i] = $"{item.Groups[1].Value}{glyph} {item.Groups[3].Value}";
        }
    }

    [GeneratedRegex(@"^\s*>\s*\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex AlertStart();

    [GeneratedRegex(@"^\s*>\s?(.*)$")]
    private static partial Regex QuoteLine();

    // Capture: (1) list marker incl. trailing space, (2) the check char, (3) the item text.
    [GeneratedRegex(@"^(\s*[-*+]\s+)\[([ xX])\]\s+(.*)$")]
    private static partial Regex TaskItem();
}
