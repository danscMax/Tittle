using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>Pure markdown text passes that bridge GitHub-flavoured syntax the renderer
/// (Markdown.Avalonia) does not handle natively into forms it does, UI-free and testable:
/// <list type="bullet">
/// <item>GitHub alerts (<c>&gt; [!NOTE]</c> …) → <c>::: &lt;type&gt;</c> container blocks,
///   rendered as themed callouts by <c>AdmonitionBlockHandler</c>.</item>
/// <item>GFM task lists (<c>- [x]</c> / <c>- [ ]</c>) → checkbox glyphs (the engine renders
///   the markers literally otherwise).</item>
/// <item>Footnotes (<c>[^id]</c> refs + <c>[^id]:</c> defs) → superscript markers + an
///   appended «Сноски» section.</item>
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

        lines = ConvertFootnotes(lines);
        lines = ConvertAdmonitions(lines);
        ConvertTaskListsInPlace(lines);

        return string.Join("\n", lines);
    }

    // Footnotes: pull out [^id]: definitions, replace [^id] references with superscript
    // numbers (numbered by first reference), and append a "Сноски" section. Anchored
    // navigation isn't attempted — the superscript ties the marker to the numbered list.
    private static List<string> ConvertFootnotes(List<string> lines)
    {
        var defs = new Dictionary<string, string>(); // default string comparer is ordinal
        var body = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            var def = FootnoteDef().Match(line);
            if (def.Success)
                defs[def.Groups[1].Value] = def.Groups[2].Value;
            else
                body.Add(line);
        }

        if (defs.Count == 0)
            return lines; // no definitions → leave any [^id] as authored

        var order = new List<string>();
        var numberOf = new Dictionary<string, int>();

        for (var i = 0; i < body.Count; i++)
        {
            body[i] = FootnoteRef().Replace(body[i], m =>
            {
                var id = m.Groups[1].Value;
                if (!numberOf.TryGetValue(id, out var n))
                {
                    n = order.Count + 1;
                    numberOf[id] = n;
                    order.Add(id);
                }
                return Superscript(n);
            });
        }

        if (order.Count == 0)
            return lines; // definitions but nothing references them → leave as authored

        body.Add(string.Empty);
        body.Add("---");
        body.Add(string.Empty);
        body.Add("**Сноски**");
        body.Add(string.Empty);
        foreach (var id in order)
            body.Add($"{numberOf[id]}. {(defs.TryGetValue(id, out var t) ? t : string.Empty)}");

        return body;
    }

    private static string Superscript(int n)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var text = n.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
            sb.Append(digits[ch - '0']);
        return sb.ToString();
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

    // A footnote definition line: [^id]: text. Capture (1) id, (2) text.
    [GeneratedRegex(@"^\[\^([^\]]+)\]:\s?(.*)$")]
    private static partial Regex FootnoteDef();

    // An inline footnote reference: [^id]. Capture (1) id.
    [GeneratedRegex(@"\[\^([^\]]+)\]")]
    private static partial Regex FootnoteRef();
}
