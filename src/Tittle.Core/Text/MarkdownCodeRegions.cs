using System.Text.RegularExpressions;

namespace Tittle.Core.Text;

/// <summary>Code-awareness for line-based markdown passes (M10): which lines sit inside fenced
/// code blocks, plus inline code-span masking for per-line replacements — so display transforms
/// never rewrite code. CommonMark-conservative: ``` / ~~~ fences with info strings (a backtick
/// fence's info string may not contain a backtick), the closer matches the opening char with at
/// least its length, up to 3 leading spaces, unclosed fences run to EOF. Built reusable so the
/// older preprocessor passes can adopt the same guard later.</summary>
public sealed partial class MarkdownCodeRegions
{
    private readonly bool[] _fenced;

    private MarkdownCodeRegions(bool[] fenced) => _fenced = fenced;

    /// <summary>Classify <paramref name="lines"/> in one forward pass. Fence delimiter lines
    /// count as fenced too, and so do <c>::: math</c> container bodies — they carry raw LaTeX
    /// the text passes must never rewrite (M11). Other containers stay transformable.</summary>
    public static MarkdownCodeRegions Scan(IReadOnlyList<string> lines)
    {
        var fenced = new bool[lines.Count];
        var inFence = false;
        var inMath = false;
        var fenceChar = '\0';
        var openLength = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            if (inFence)
            {
                fenced[i] = true;
                var close = FenceClose().Match(lines[i]);
                if (close.Success
                    && close.Groups["fence"].Value[0] == fenceChar
                    && close.Groups["fence"].Length >= openLength)
                    inFence = false;
                continue;
            }

            if (inMath)
            {
                fenced[i] = true;
                if (ContainerClose().IsMatch(lines[i]))
                    inMath = false;
                continue;
            }

            if (MathContainerOpen().IsMatch(lines[i]))
            {
                fenced[i] = true;
                inMath = true;
                continue;
            }

            var open = FenceOpen().Match(lines[i]);
            if (!open.Success)
                continue;

            var run = open.Groups["fence"].Value;
            if (run[0] == '`' && open.Groups["info"].Value.Contains('`'))
                continue; // not a fence: backtick info strings may not contain backticks

            inFence = true;
            fenceChar = run[0];
            openLength = run.Length;
            fenced[i] = true;
        }

        return new MarkdownCodeRegions(fenced);
    }

    /// <summary>True when line <paramref name="index"/> is a fence delimiter or fenced content.</summary>
    public bool IsFencedLine(int index) => index >= 0 && index < _fenced.Length && _fenced[index];

    /// <summary>Run <paramref name="pattern"/>.Replace over <paramref name="line"/>, suppressing
    /// any match that OVERLAPS an inline code span (or an interval matched by one of
    /// <paramref name="extraMasks"/>) — straddling matches are never rewritten.</summary>
    public static string ReplaceOutsideCode(
        string line, Regex pattern, MatchEvaluator evaluator, params Regex[] extraMasks)
    {
        var masked = new List<(int Start, int End)>();
        CollectMatches(InlineCodeSpan(), line, masked);
        foreach (var mask in extraMasks)
            CollectMatches(mask, line, masked);

        return masked.Count == 0
            ? pattern.Replace(line, evaluator)
            : pattern.Replace(line, m => Overlaps(masked, m.Index, m.Index + m.Length) ? m.Value : evaluator(m));
    }

    private static void CollectMatches(Regex regex, string line, List<(int Start, int End)> into)
    {
        foreach (Match m in regex.Matches(line))
            into.Add((m.Index, m.Index + m.Length));
    }

    private static bool Overlaps(List<(int Start, int End)> intervals, int start, int end)
    {
        foreach (var (s, e) in intervals)
        {
            if (start < e && s < end)
                return true;
        }

        return false;
    }

    // Fence opener: ``` or ~~~ (3+), up to 3 leading spaces, optional info string.
    [GeneratedRegex(@"^ {0,3}(?<fence>`{3,}|~{3,})(?<info>.*)$")]
    private static partial Regex FenceOpen();

    // Fence closer: a bare run (same char, >= opening length checked in code), only whitespace after.
    [GeneratedRegex(@"^ {0,3}(?<fence>`{3,}|~{3,})[ \t]*$")]
    private static partial Regex FenceClose();

    // Inline code span: a backtick run closed by an equal run (mirrors the renderer's pattern,
    // so masking agrees with what it renders as code). Per-line — multi-line spans are a known
    // limitation of the line-based preprocessor.
    [GeneratedRegex(@"(?<!\\)(?<!`)(`+)(.+?)(?<!`)\1(?!`)")]
    private static partial Regex InlineCodeSpan();

    // The math container emitted by the preprocessor's own math pass (M11).
    [GeneratedRegex(@"^\s*::: math\s*$")]
    private static partial Regex MathContainerOpen();

    [GeneratedRegex(@"^\s*:::\s*$")]
    private static partial Regex ContainerClose();
}
