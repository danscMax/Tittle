using System.Collections;

namespace SeriousView.Core.Text;

/// <summary>Scroll anchor: the nearest heading at/above a position (−1 = before the first
/// heading, also when there are none) plus fractional progress toward the next boundary.
/// Lets preview pixels and source lines describe "the same place" in a document, so the
/// view-mode toggle can land where the reader was (M10). Percentage-of-document sync was
/// rejected — it drifts on long documents whose preview/source heights diverge.</summary>
public readonly record struct HeadingAnchor(int Ordinal, double Fraction);

/// <summary>Pure anchor maths over a 1-D axis segmented by heading boundaries:
/// document start → h₀ → h₁ → … → end. The pixel (preview) and line (source) views share
/// the same functions — lines map onto the axis as x = line − 1 with end = totalLines, so
/// zero headings degrades to plain proportional sync (one segment) with no special case.</summary>
public static class HeadingAnchors
{
    /// <summary>Anchor for <paramref name="position"/> on an axis with ascending heading
    /// positions <paramref name="headingTops"/> that ends at <paramref name="end"/>.</summary>
    public static HeadingAnchor FromPosition(IReadOnlyList<double> headingTops, double position, double end)
    {
        var pos = Math.Clamp(position, 0, Math.Max(0, end));
        var ordinal = LastAtOrBelow(headingTops, pos, epsilon: 0);
        var (segStart, segEnd) = Segment(headingTops, ordinal, end);
        var fraction = segEnd > segStart ? Math.Clamp((pos - segStart) / (segEnd - segStart), 0, 1) : 0;
        return new HeadingAnchor(ordinal, fraction);
    }

    /// <summary>Position of <paramref name="anchor"/> on the axis. Out-of-range ordinals and
    /// fractions clamp, so a stale anchor still lands somewhere sensible.</summary>
    public static double ToPosition(IReadOnlyList<double> headingTops, HeadingAnchor anchor, double end)
    {
        var ordinal = Math.Clamp(anchor.Ordinal, -1, headingTops.Count - 1);
        var (segStart, segEnd) = Segment(headingTops, ordinal, end);
        var fraction = Math.Clamp(anchor.Fraction, 0, 1);
        return Math.Clamp(segStart + fraction * (segEnd - segStart), 0, Math.Max(0, end));
    }

    /// <summary>Active heading for scroll-spy: the last heading whose top is at/above the probe
    /// position. <paramref name="epsilon"/> makes the boundary inclusive, so a heading scrolled
    /// exactly to the probe line counts as active. −1 above the first heading.</summary>
    public static int ActiveOrdinal(IReadOnlyList<double> headingTops, double position, double epsilon = 1.0)
        => LastAtOrBelow(headingTops, position, epsilon);

    /// <summary>Line-axis adapter of <see cref="FromPosition"/> (x = line − 1, end = totalLines).</summary>
    public static HeadingAnchor FromLine(IReadOnlyList<HeadingOutline> outline, int line, int totalLines)
        => FromPosition(new LineTops(outline), line - 1, totalLines);

    /// <summary>Line-axis adapter of <see cref="ToPosition"/>; result is a valid 1-based line.</summary>
    public static int ToLine(IReadOnlyList<HeadingOutline> outline, HeadingAnchor anchor, int totalLines)
    {
        if (totalLines < 1)
            return 1;
        var pos = ToPosition(new LineTops(outline), anchor, totalLines);
        return Math.Clamp((int)Math.Round(pos) + 1, 1, totalLines);
    }

    /// <summary>Active heading for the source view: the last heading at/above the first visible
    /// line. Allocation-free — runs on every editor scroll event.</summary>
    public static int ActiveOrdinalForLine(IReadOnlyList<HeadingOutline> outline, int firstVisibleLine)
    {
        int lo = 0, hi = outline.Count - 1, last = -1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (outline[mid].Line <= firstVisibleLine)
            {
                last = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return last;
    }

    /// <summary>Binary search: index of the last top ≤ position + epsilon, or −1.</summary>
    private static int LastAtOrBelow(IReadOnlyList<double> tops, double position, double epsilon)
    {
        var limit = position + epsilon;
        int lo = 0, hi = tops.Count - 1, last = -1;
        while (lo <= hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (tops[mid] <= limit)
            {
                last = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return last;
    }

    /// <summary>The segment containing <paramref name="ordinal"/> (−1 = the document-start
    /// segment). A malformed axis (end before the last top) yields a degenerate segment,
    /// which callers treat as fraction 0 — never a crash.</summary>
    private static (double Start, double End) Segment(IReadOnlyList<double> tops, int ordinal, double end)
    {
        var start = ordinal >= 0 ? tops[ordinal] : 0;
        var next = ordinal + 1 < tops.Count ? tops[ordinal + 1] : Math.Max(end, start);
        return (start, next);
    }

    /// <summary>Zero-copy view of outline heading lines as axis positions (line − 1).</summary>
    private sealed class LineTops(IReadOnlyList<HeadingOutline> outline) : IReadOnlyList<double>
    {
        public double this[int index] => outline[index].Line - 1;
        public int Count => outline.Count;

        public IEnumerator<double> GetEnumerator()
        {
            for (var i = 0; i < outline.Count; i++)
                yield return outline[i].Line - 1;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
