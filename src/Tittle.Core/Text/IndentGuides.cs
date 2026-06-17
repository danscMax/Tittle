using System;
using System.Collections.Generic;

namespace Tittle.Core.Text;

/// <summary>Pure indent-guide geometry for the code view (ported): which vertical guide
/// columns a line carries, with tab expansion and blank lines bridging the surrounding block.</summary>
public static class IndentGuides
{
    /// <summary>How far down/up a blank line looks for its non-blank neighbours.</summary>
    public const int BlankScanLimit = 100;

    /// <summary>Visual column where the leading whitespace ends (tabs advance to the next
    /// stop), or -1 when the line is blank/whitespace-only.</summary>
    public static int LeadingColumns(string line, int tabSize)
    {
        var col = 0;
        foreach (var ch in line)
        {
            if (ch == ' ')
                col++;
            else if (ch == '\t')
                col += tabSize - (col % tabSize);
            else
                return col;
        }

        return -1;
    }

    /// <summary>Indent of a line for guide purposes: its own leading columns, or — for a blank
    /// line — the shallower of the nearest non-blank neighbours (so guides continue through
    /// blank lines inside a block but stop where the block ends). <paramref name="lineAt"/>
    /// returns the 1-based line's text, or null outside the document.</summary>
    public static int EffectiveColumns(Func<int, string?> lineAt, int lineNumber, int tabSize)
    {
        var own = lineAt(lineNumber) is { } text ? LeadingColumns(text, tabSize) : -1;
        if (own >= 0)
            return own;

        var above = ScanNeighbour(lineAt, lineNumber, -1, tabSize);
        var below = ScanNeighbour(lineAt, lineNumber, +1, tabSize);
        if (above < 0 || below < 0)
            return 0; // a blank run at the document edge belongs to no block

        return Math.Min(above, below);
    }

    /// <summary>Guide columns strictly inside an indent: every tab stop above 0 and below
    /// <paramref name="leadingColumns"/> (column 0 is the margin; the last stop is the text).</summary>
    public static IReadOnlyList<int> GuideColumnsFor(int leadingColumns, int tabSize)
    {
        if (tabSize <= 0 || leadingColumns <= tabSize)
            return Array.Empty<int>();

        var stops = new List<int>();
        for (var col = tabSize; col < leadingColumns; col += tabSize)
            stops.Add(col);
        return stops;
    }

    private static int ScanNeighbour(Func<int, string?> lineAt, int from, int step, int tabSize)
    {
        for (var i = 1; i <= BlankScanLimit; i++)
        {
            if (lineAt(from + step * i) is not { } text)
                return -1;
            var cols = LeadingColumns(text, tabSize);
            if (cols >= 0)
                return cols;
        }

        return -1;
    }
}
