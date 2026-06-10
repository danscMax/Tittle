using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>A heading extracted from markdown source, for the outline / table of contents.</summary>
/// <param name="Text">Heading text with the ATX markers stripped.</param>
/// <param name="Level">Heading depth 1–6 (number of leading <c>#</c>).</param>
/// <param name="Line">1-based source line — used to scroll the source editor.</param>
/// <param name="Ordinal">0-based index among the document's headings — used to scroll the preview.</param>
public sealed record HeadingOutline(string Text, int Level, int Line, int Ordinal);

/// <summary>Pure markdown heading parser (UI-free, testable). Extracts ATX headings
/// (<c>#</c> … <c>######</c>) at the start of a line, skipping any inside fenced code blocks
/// (<c>```</c> or <c>~~~</c>). Setext headings (underlined with <c>===</c>/<c>---</c>) are not
/// parsed — GFM recommends ATX and they are ambiguous with thematic breaks / table separators.</summary>
public static partial class MarkdownOutline
{
    public static IReadOnlyList<HeadingOutline> Parse(string? markdown)
    {
        var headings = new List<HeadingOutline>();
        if (string.IsNullOrEmpty(markdown))
            return headings;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? fence = null; // the opening fence run (``` or ~~~) while inside a code block

        for (var i = 0; i < lines.Length; i++)
        {
            var fenceMatch = Fence().Match(lines[i]);
            if (fenceMatch.Success)
            {
                var run = fenceMatch.Groups[1];
                if (fence is null)
                    fence = run.Value;                               // open (info string allowed)
                else if (run.Value[0] == fence[0] && run.Length >= fence.Length
                         && lines[i].AsSpan(run.Index + run.Length).Trim().IsEmpty)
                    fence = null;                                    // close: same char, ≥ length, nothing after
                continue;
            }

            if (fence is not null)
                continue; // inside a code block — '#' lines are not headings

            var head = Heading().Match(lines[i]);
            if (!head.Success)
                continue;

            // Strip an optional ATX closing sequence (whitespace + #s), but keep a trailing '#'
            // that is part of the text (e.g. "C#"), which has no preceding space.
            var text = ClosingHashes().Replace(head.Groups[2].Value, "").Trim();
            headings.Add(new HeadingOutline(text, head.Groups[1].Value.Length, i + 1, headings.Count));
        }

        return headings;
    }

    /// <summary>Ancestor chain for breadcrumbs (M10): the active heading and, walking backwards,
    /// the nearest heading of each strictly smaller level — top-level first. Empty for −1 /
    /// out-of-range. Level jumps stay as authored (H1→H3 with no H2 yields [H1, H3]); a later
    /// same-or-higher-level heading resets the chain naturally.</summary>
    public static IReadOnlyList<HeadingOutline> AncestorChain(
        IReadOnlyList<HeadingOutline> outline, int activeOrdinal)
    {
        if (activeOrdinal < 0 || activeOrdinal >= outline.Count)
            return [];

        var chain = new List<HeadingOutline> { outline[activeOrdinal] };
        var minLevel = outline[activeOrdinal].Level;
        for (var i = activeOrdinal - 1; i >= 0 && minLevel > 1; i--)
        {
            if (outline[i].Level < minLevel)
            {
                chain.Add(outline[i]);
                minLevel = outline[i].Level;
            }
        }

        chain.Reverse();
        return chain;
    }

    // Fenced code delimiter: ``` or ~~~ (3+), indented up to 3 spaces, optional info string after.
    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex Fence();

    // ATX heading: up to 3 leading spaces, 1–6 '#', a space, then text. Capture (1) hashes, (2) text.
    [GeneratedRegex(@"^ {0,3}(#{1,6})\s+(.*)$")]
    private static partial Regex Heading();

    // Trailing ATX closing sequence: whitespace then a run of '#' to end of line.
    [GeneratedRegex(@"\s+#+\s*$")]
    private static partial Regex ClosingHashes();
}
