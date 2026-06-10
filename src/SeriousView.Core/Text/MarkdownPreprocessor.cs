using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>Pure markdown text passes that bridge GitHub-flavoured syntax the renderer
/// (Markdown.Avalonia) does not handle natively into forms it does, UI-free and testable:
/// <list type="bullet">
/// <item>Wiki links (<c>[[name]]</c>) → markdown links to <c>wiki:</c> URLs when the injected
///   resolver knows the sibling note, else plain text (M10).</item>
/// <item>GitHub alerts (<c>&gt; [!NOTE]</c> …) → <c>::: &lt;type&gt;</c> container blocks,
///   rendered as themed callouts by <c>AdmonitionBlockHandler</c>.</item>
/// <item>GFM task lists (<c>- [x]</c> / <c>- [ ]</c>) → checkbox glyphs (the engine renders
///   the markers literally otherwise).</item>
/// <item>Footnotes (<c>[^id]</c> refs + <c>[^id]:</c> defs) → superscript markers + an
///   appended «Сноски» section.</item>
/// </list></summary>
public static partial class MarkdownPreprocessor
{
    /// <summary>Per-line cap for the inline passes — keeps any regex worst case bounded on
    /// hostile single-line documents; longer lines pass through untransformed.</summary>
    private const int MaxInlineLineLength = 10_000;

    /// <summary>Apply all markdown-normalising passes. Returns the input unchanged when there
    /// is nothing to transform (plain markdown round-trips, modulo CRLF → LF normalisation).</summary>
    public static string Transform(string? markdown) => Transform(markdown, null);

    /// <summary>Full pipeline. <paramref name="wikiLinkResolver"/> receives a trimmed wiki name
    /// (no <c>.md</c>) and answers whether a sibling note with that name exists; it must not
    /// throw and is consulted once per distinct name (memoized). Null = nothing resolves, so
    /// every <c>[[name]]</c> degrades to plain text.</summary>
    public static string Transform(string? markdown, Func<string, bool>? wikiLinkResolver)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown ?? string.Empty;

        var lines = new List<string>(
            markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));

        // Inline passes run first, in place (line count preserved → the fence bitmap stays
        // valid) and before admonition re-wrapping so callout bodies get them too. Wiki before
        // underscore: a [[my_note]] link resolves to its real file first, and the underscore
        // pass then sees its destination behind the link mask.
        var regions = MarkdownCodeRegions.Scan(lines);
        ConvertWikiLinksInPlace(lines, regions, Memoize(wikiLinkResolver));
        ConvertUnderscoreEmphasisInPlace(lines, regions);

        // The legacy passes are fence-guarded too; footnotes/admonitions REBUILD the line list,
        // so the fence bitmap is rescanned after each of them (Scan is one cheap O(n) pass).
        lines = ConvertFootnotes(lines, regions);
        regions = MarkdownCodeRegions.Scan(lines);
        lines = ConvertAdmonitions(lines, regions);
        regions = MarkdownCodeRegions.Scan(lines);
        ConvertTaskListsInPlace(lines, regions);

        return string.Join("\n", lines);
    }

    // [[name]] → "[name](wiki:<encoded>)" when the resolver knows the note, else plain "name".
    // [[a|b]] / [[ ]] don't match the token and stay as authored. Skips fenced lines, inline
    // code spans, link-reference-definition lines and overlong lines.
    private static void ConvertWikiLinksInPlace(
        List<string> lines, MarkdownCodeRegions regions, Func<string, bool>? resolve)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (regions.IsFencedLine(i) || line.Length > MaxInlineLineLength
                || !line.Contains("[[", StringComparison.Ordinal) || LinkRefDefLine().IsMatch(line))
                continue;

            lines[i] = MarkdownCodeRegions.ReplaceOutsideCode(line, WikiToken(), m =>
            {
                var name = m.Groups[1].Value.Trim();
                if (name.Length == 0)
                    return m.Value;
                if (!WikiLink.IsValidName(name) || resolve?.Invoke(name) != true)
                    return name;
                return $"[{name}]({WikiLink.CreateUrl(name)})";
            });
        }
    }

    // _x_ → *x* (display-only): the renderer has no single-underscore italics, while its
    // __x__ renders as UNDERLINE natively (verified against Markdown.Avalonia 11.0.3) — so
    // double/triple runs are deliberately untouched. CommonMark-conservative: word-boundary
    // flanks only (no intraword a_b_c; .NET \w covers '_' itself, killing run adjacency too),
    // content underscore-free. Link destinations and autolinks are masked so URLs survive.
    private static void ConvertUnderscoreEmphasisInPlace(List<string> lines, MarkdownCodeRegions regions)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (regions.IsFencedLine(i) || line.Length > MaxInlineLineLength
                || !line.Contains('_') || LinkRefDefLine().IsMatch(line))
                continue;

            lines[i] = MarkdownCodeRegions.ReplaceOutsideCode(
                line, UnderscoreEmphasis(), m => $"*{m.Groups[1].Value}*",
                LinkDestination(), AutoLink());
        }
    }

    /// <summary>One resolver hit per distinct name per Transform — a note linked many times
    /// costs one existence check.</summary>
    private static Func<string, bool>? Memoize(Func<string, bool>? resolver)
    {
        if (resolver is null)
            return null;

        var known = new Dictionary<string, bool>(); // default comparer is ordinal
        return name => known.TryGetValue(name, out var exists) ? exists : known[name] = resolver(name);
    }

    // Footnotes: pull out [^id]: definitions, replace [^id] references with superscript
    // numbers (numbered by first reference), and append a "Сноски" section. Anchored
    // navigation isn't attempted — the superscript ties the marker to the numbered list.
    private static List<string> ConvertFootnotes(List<string> lines, MarkdownCodeRegions regions)
    {
        var defs = new Dictionary<string, string>(); // default string comparer is ordinal
        var body = new List<string>(lines.Count);
        var bodyFenced = new List<bool>(lines.Count); // fenced body lines keep their [^id]s
        for (var i = 0; i < lines.Count; i++)
        {
            var fenced = regions.IsFencedLine(i);
            if (!fenced)
            {
                var def = FootnoteDef().Match(lines[i]);
                if (def.Success)
                {
                    defs[def.Groups[1].Value] = def.Groups[2].Value;
                    continue;
                }
            }

            body.Add(lines[i]);
            bodyFenced.Add(fenced);
        }

        if (defs.Count == 0)
            return lines; // no definitions → leave any [^id] as authored

        var order = new List<string>();
        var numberOf = new Dictionary<string, int>();

        for (var i = 0; i < body.Count; i++)
        {
            if (bodyFenced[i])
                continue;

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
    private static List<string> ConvertAdmonitions(List<string> lines, MarkdownCodeRegions regions)
    {
        var result = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            // A "> [!NOTE]" line inside a fence is code, not an alert opener (the body walk
            // below is not fence-guarded — a fence starting mid-callout isn't a real shape).
            var start = regions.IsFencedLine(i) ? Match.Empty : AlertStart().Match(lines[i]);
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

    private static void ConvertTaskListsInPlace(List<string> lines, MarkdownCodeRegions regions)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (regions.IsFencedLine(i))
                continue; // "- [x]" inside a fence is code

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

    // A wiki-link token: [[name]] — no nesting, no pipe, non-empty. Needs two literal '[',
    // so it can never collide with footnote [^id] syntax.
    [GeneratedRegex(@"\[\[([^\[\]|]+)\]\]")]
    private static partial Regex WikiToken();

    // A link-reference definition line ("[label]: dest") — skipped by the inline passes; the
    // (?!\^) keeps footnote DEFINITION text eligible (it becomes visible «Сноски» content).
    [GeneratedRegex(@"^ {0,3}\[(?!\^)[^\]]+\]:")]
    private static partial Regex LinkRefDefLine();

    // Single-underscore emphasis around a whole "word": flanks must not be word chars (\w
    // includes '_', so adjacency to other underscores is excluded too); content has no
    // underscores and no leading/trailing whitespace. Capture (1) = the emphasised text.
    [GeneratedRegex(@"(?<!\w)_(?![\s_])([^_\n]*[^\s_])_(?!\w)")]
    private static partial Regex UnderscoreEmphasis();

    // Mask: an inline link/image destination "](…)" — protects URLs (incl. wiki: ones).
    [GeneratedRegex(@"\]\([^)\n]*\)")]
    private static partial Regex LinkDestination();

    // Mask: an autolink "<scheme:…>".
    [GeneratedRegex(@"<[a-zA-Z][a-zA-Z0-9+.\-]*:[^<>\s]*>")]
    private static partial Regex AutoLink();
}
