using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class IndentGuidesTests
{
    [Theory]
    [InlineData("no indent", 4, 0)]
    [InlineData("    four spaces", 4, 4)]
    [InlineData("        eight", 4, 8)]
    [InlineData("  two", 4, 2)]
    public void LeadingColumns_CountsSpaces(string line, int tabSize, int expected)
        => Assert.Equal(expected, IndentGuides.LeadingColumns(line, tabSize));

    [Theory]
    [InlineData("\tx", 4, 4)]      // tab advances to the next stop
    [InlineData("\t\tx", 4, 8)]
    [InlineData("  \tx", 4, 4)]    // 2 spaces + tab → stop at 4, not 6
    [InlineData("\t x", 4, 5)]
    public void LeadingColumns_ExpandsTabsToStops(string line, int tabSize, int expected)
        => Assert.Equal(expected, IndentGuides.LeadingColumns(line, tabSize));

    [Theory]
    [InlineData("", 4)]
    [InlineData("   ", 4)]
    [InlineData("\t", 4)]
    public void LeadingColumns_BlankLines_ReportMinusOne(string line, int tabSize)
        => Assert.Equal(-1, IndentGuides.LeadingColumns(line, tabSize));

    private static string?[] Doc => new string?[]
    {
        "def f():",        // 1
        "    if x:",       // 2
        "        a()",     // 3
        "",                // 4 — blank inside the nested block
        "        b()",     // 5
        "    return",      // 6
    };

    private static string? LineAt(int n) => n >= 1 && n <= Doc.Length ? Doc[n - 1] : null;

    [Fact]
    public void EffectiveColumns_NonBlankLine_IsItsOwnIndent()
        => Assert.Equal(8, IndentGuides.EffectiveColumns(LineAt, 3, 4));

    [Fact]
    public void EffectiveColumns_BlankLine_BridgesNeighbours()
        // Blank line 4 sits between two 8-column lines → guides continue at 8.
        => Assert.Equal(8, IndentGuides.EffectiveColumns(LineAt, 4, 4));

    [Fact]
    public void EffectiveColumns_BlankLine_TakesTheShallowerNeighbour()
    {
        // Blank between 8-col and 4-col lines → the block ends; only the 4-col guide continues.
        string?[] doc = { "        a()", "", "    return" };
        string? At(int n) => n >= 1 && n <= doc.Length ? doc[n - 1] : null;

        Assert.Equal(4, IndentGuides.EffectiveColumns(At, 2, 4));
    }

    [Fact]
    public void EffectiveColumns_BlankAtTheEdge_IsZero()
    {
        string?[] doc = { "", "x" };
        string? At(int n) => n >= 1 && n <= doc.Length ? doc[n - 1] : null;

        // No non-blank line above → nothing to bridge.
        Assert.Equal(0, IndentGuides.EffectiveColumns(At, 1, 4));
    }

    [Fact]
    public void GuideColumnsFor_YieldsTabStopsStrictlyInsideTheIndent()
        // 8 leading columns at tab 4 → one guide at column 4 (0 is the margin, 8 is the text).
        => Assert.Equal(new[] { 4 }, IndentGuides.GuideColumnsFor(8, 4));

    [Fact]
    public void GuideColumnsFor_DeepIndent_YieldsEveryStop()
        => Assert.Equal(new[] { 4, 8, 12 }, IndentGuides.GuideColumnsFor(14, 4));

    [Fact]
    public void GuideColumnsFor_ShallowIndent_YieldsNothing()
        => Assert.Empty(IndentGuides.GuideColumnsFor(3, 4));
}
