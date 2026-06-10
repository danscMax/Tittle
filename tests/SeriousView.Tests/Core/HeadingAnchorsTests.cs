using System;
using System.Collections.Generic;
using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class HeadingAnchorsTests
{
    /// <summary>Outline with headings at the given 1-based lines (level/text irrelevant here).</summary>
    private static IReadOnlyList<HeadingOutline> OutlineAt(params int[] lines)
        => lines.Select((l, i) => new HeadingOutline($"H{i}", 1, l, i)).ToList();

    // --- Line axis: FromLine ---

    [Fact]
    public void FromLine_OnHeadingLine_FractionZero()
    {
        var a = HeadingAnchors.FromLine(OutlineAt(1, 33, 66), line: 33, totalLines: 100);

        Assert.Equal(1, a.Ordinal);
        Assert.Equal(0.0, a.Fraction, 9);
    }

    [Fact]
    public void FromLine_BetweenHeadings_FractionalProgress()
    {
        // Segment [line 1, line 33) → positions [0, 32); line 17 → 16/32 = 0.5.
        var a = HeadingAnchors.FromLine(OutlineAt(1, 33, 66), line: 17, totalLines: 100);

        Assert.Equal(0, a.Ordinal);
        Assert.Equal(0.5, a.Fraction, 9);
    }

    [Fact]
    public void FromLine_BeforeFirstHeading_OrdinalMinusOne()
    {
        // Document start is its own segment [line 1, line 10) → line 5 = 4/9 through it.
        var a = HeadingAnchors.FromLine(OutlineAt(10, 50), line: 5, totalLines: 100);

        Assert.Equal(-1, a.Ordinal);
        Assert.Equal(4.0 / 9, a.Fraction, 9);
    }

    [Fact]
    public void FromLine_AfterLastHeading_ProgressTowardDocumentEnd()
    {
        // Last segment [line 66, end=100 lines) → line 100 = 34/35 through it.
        var a = HeadingAnchors.FromLine(OutlineAt(1, 33, 66), line: 100, totalLines: 100);

        Assert.Equal(2, a.Ordinal);
        Assert.Equal(34.0 / 35, a.Fraction, 9);
    }

    [Fact]
    public void FromLine_NoHeadings_DegradesToProportional()
    {
        var a = HeadingAnchors.FromLine(OutlineAt(), line: 51, totalLines: 100);

        Assert.Equal(-1, a.Ordinal);
        Assert.Equal(0.5, a.Fraction, 9);
    }

    [Fact]
    public void FromLine_SingleHeading_BothSegments()
    {
        var outline = OutlineAt(50);

        var before = HeadingAnchors.FromLine(outline, line: 25, totalLines: 100);
        var after = HeadingAnchors.FromLine(outline, line: 75, totalLines: 100);

        Assert.Equal(-1, before.Ordinal);
        Assert.Equal(24.0 / 49, before.Fraction, 9);
        Assert.Equal(0, after.Ordinal);
        Assert.Equal(25.0 / 51, after.Fraction, 9);
    }

    [Fact]
    public void FromLine_ClampsOutOfRangeLines()
    {
        var outline = OutlineAt(10);

        var below = HeadingAnchors.FromLine(outline, line: 0, totalLines: 100);
        var wayBelow = HeadingAnchors.FromLine(outline, line: -5, totalLines: 100);
        var above = HeadingAnchors.FromLine(outline, line: 10_000, totalLines: 100);

        Assert.Equal(HeadingAnchors.FromLine(outline, 1, 100), below);
        Assert.Equal(below, wayBelow);
        Assert.Equal(0, above.Ordinal); // clamped into the last segment
        Assert.InRange(above.Fraction, 0.0, 1.0);
    }

    // --- Line axis: ToLine ---

    [Fact]
    public void ToLine_RoundTripsHeadingLinesExactly()
    {
        var outline = OutlineAt(1, 33, 66);
        foreach (var line in new[] { 1, 33, 66 })
        {
            var back = HeadingAnchors.ToLine(outline, HeadingAnchors.FromLine(outline, line, 100), 100);
            Assert.Equal(line, back);
        }
    }

    [Fact]
    public void ToLine_RoundTrip_WithinOneLine_ForEveryLine()
    {
        var outline = OutlineAt(1, 33, 66);
        for (var line = 1; line <= 100; line++)
        {
            var back = HeadingAnchors.ToLine(outline, HeadingAnchors.FromLine(outline, line, 100), 100);
            Assert.True(Math.Abs(back - line) <= 1, $"line {line} round-tripped to {back}");
        }
    }

    [Fact]
    public void ToLine_ClampsOrdinalAndResult()
    {
        var outline = OutlineAt(1, 33, 66);

        var farOrdinal = HeadingAnchors.ToLine(outline, new HeadingAnchor(10, 0.5), 100);
        var negOrdinal = HeadingAnchors.ToLine(outline, new HeadingAnchor(-7, 0.0), 100);
        var emptyDoc = HeadingAnchors.ToLine(OutlineAt(), new HeadingAnchor(-1, 0.5), 0);

        Assert.InRange(farOrdinal, 66, 100); // clamped into the last segment
        Assert.Equal(1, negOrdinal);
        Assert.Equal(1, emptyDoc);
    }

    // --- Pixel axis ---

    [Fact]
    public void FromPosition_TiedTops_DegenerateSegment_FractionZero()
    {
        var tops = new double[] { 100, 100, 200 };

        var a = HeadingAnchors.FromPosition(tops, position: 100, end: 300);

        Assert.Equal(1, a.Ordinal); // last top <= position wins
        Assert.Equal(0.0, a.Fraction, 9);
    }

    [Fact]
    public void FromPosition_EndAtLastTop_DoesNotThrow()
    {
        var tops = new double[] { 0, 100 };

        var a = HeadingAnchors.FromPosition(tops, position: 150, end: 100); // inconsistent input

        Assert.Equal(1, a.Ordinal);
        Assert.Equal(0.0, a.Fraction, 9); // degenerate last segment
    }

    [Fact]
    public void ToPosition_RoundTripsAndClamps()
    {
        var tops = new double[] { 0, 32, 65 };

        var mid = HeadingAnchors.ToPosition(tops, new HeadingAnchor(0, 0.5), end: 100);
        var clampedHigh = HeadingAnchors.ToPosition(tops, new HeadingAnchor(10, 0.5), end: 100);
        var beforeFirst = HeadingAnchors.ToPosition(tops, new HeadingAnchor(-1, 0.0), end: 100);

        Assert.Equal(16.0, mid, 9);
        Assert.Equal(65 + 17.5, clampedHigh, 9);
        Assert.Equal(0.0, beforeFirst, 9);
    }

    // --- Active ordinal ---

    [Fact]
    public void ActiveOrdinal_EpsilonMakesBoundaryInclusive()
    {
        var tops = new double[] { 0, 32, 65 };

        Assert.Equal(1, HeadingAnchors.ActiveOrdinal(tops, position: 31, epsilon: 1.0)); // 32 <= 31+1
        Assert.Equal(0, HeadingAnchors.ActiveOrdinal(tops, position: 30, epsilon: 1.0));
        Assert.Equal(2, HeadingAnchors.ActiveOrdinal(tops, position: 1000, epsilon: 1.0));
    }

    [Fact]
    public void ActiveOrdinal_BeforeFirstHeading_MinusOne()
    {
        Assert.Equal(-1, HeadingAnchors.ActiveOrdinal(new double[] { 10, 20 }, position: 5, epsilon: 1.0));
        Assert.Equal(-1, HeadingAnchors.ActiveOrdinal(Array.Empty<double>(), position: 5, epsilon: 1.0));
    }

    [Fact]
    public void ActiveOrdinalForLine_LastHeadingAtOrAboveTheLine()
    {
        var outline = OutlineAt(5, 10);

        Assert.Equal(0, HeadingAnchors.ActiveOrdinalForLine(outline, firstVisibleLine: 7));
        Assert.Equal(1, HeadingAnchors.ActiveOrdinalForLine(outline, firstVisibleLine: 10));
        Assert.Equal(-1, HeadingAnchors.ActiveOrdinalForLine(outline, firstVisibleLine: 3));
    }
}
