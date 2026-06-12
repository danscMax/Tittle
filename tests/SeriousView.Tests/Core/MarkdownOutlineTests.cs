using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class MarkdownOutlineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No headings here\njust text")]
    [InlineData("#no space is not a heading")]
    [InlineData("####### H")]   // 7 '#' exceeds the ATX max of 6 → not a heading
    public void Parse_NoHeadings_ReturnsEmpty(string? input)
        => Assert.Empty(MarkdownOutline.Parse(input));

    [Fact]
    public void Parse_SingleHeading_CapturesTextLevelLineOrdinal()
    {
        var result = MarkdownOutline.Parse("intro\n# Title\nbody");

        var h = Assert.Single(result);
        Assert.Equal("Title", h.Text);
        Assert.Equal(1, h.Level);
        Assert.Equal(2, h.Line);     // 1-based
        Assert.Equal(0, h.Ordinal);
    }

    [Theory]
    [InlineData("# H", 1)]
    [InlineData("## H", 2)]
    [InlineData("### H", 3)]
    [InlineData("#### H", 4)]
    [InlineData("##### H", 5)]
    [InlineData("###### H", 6)]
    public void Parse_Level_MatchesHashCount(string md, int expectedLevel)
        => Assert.Equal(expectedLevel, MarkdownOutline.Parse(md)[0].Level);

    [Fact]
    public void Parse_StripsClosingSequence_ButKeepsTrailingHashInText()
    {
        Assert.Equal("Heading", MarkdownOutline.Parse("##   Heading   ").Single().Text);
        Assert.Equal("Heading", MarkdownOutline.Parse("## Heading ##").Single().Text);
        Assert.Equal("C#", MarkdownOutline.Parse("# C#").Single().Text);          // no space before #
        Assert.Equal("F# notes", MarkdownOutline.Parse("## F# notes").Single().Text);
    }

    [Fact]
    public void Parse_MultipleHeadings_InOrderWithOrdinals()
    {
        const string md = """
            # First

            text

            ## Second
            ### Third
            """;
        var result = MarkdownOutline.Parse(md);

        Assert.Equal(3, result.Count);
        Assert.Equal(("First", 1, 1, 0), (result[0].Text, result[0].Level, result[0].Line, result[0].Ordinal));
        Assert.Equal(("Second", 2, 5, 1), (result[1].Text, result[1].Level, result[1].Line, result[1].Ordinal));
        Assert.Equal(("Third", 3, 6, 2), (result[2].Text, result[2].Level, result[2].Line, result[2].Ordinal));
    }

    [Fact]
    public void Parse_HeadingLineNumbers_SurviveMultipleMathBlocks()
    {
        // Q20: $$ blocks expand into blank-padded ::: containers in the PREVIEW preprocessor, but the
        // outline (and the source-scroll it drives) is built from the RAW text — so several math blocks
        // must not shift downstream heading line numbers. Headings stay at their authored 1-based lines.
        const string md = """
            # H1

            $$
            a = b
            $$

            ## H2

            $$
            c = d
            $$

            ### H3
            """;
        var result = MarkdownOutline.Parse(md);

        Assert.Equal(3, result.Count);
        Assert.Equal(("H1", 1), (result[0].Text, result[0].Line));
        Assert.Equal(("H2", 7), (result[1].Text, result[1].Line));
        Assert.Equal(("H3", 13), (result[2].Text, result[2].Line));
    }

    [Fact]
    public void Parse_SkipsHeadingsInsideFencedBlocks()
    {
        const string md = """
            # Real

            ```python
            # not a heading
            ## also not
            ```

            ## After
            """;
        var result = MarkdownOutline.Parse(md);

        Assert.Equal(2, result.Count);
        Assert.Equal("Real", result[0].Text);
        Assert.Equal("After", result[1].Text);
    }

    [Fact]
    public void Parse_TildeFence_AlsoSkips()
    {
        var result = MarkdownOutline.Parse("# A\n~~~\n# inside\n~~~\n# B");

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Text);
        Assert.Equal("B", result[1].Text);
    }

    [Fact]
    public void Parse_FenceWithInfoStringInsideBlock_DoesNotFalselyClose()
    {
        // A ```lang line inside an open block is not a closing fence (close takes no info
        // string), so headings after it stay inside the code block and out of the outline.
        const string md = """
            # A

            ```
            sample:
            ```text
            # still inside the code block
            ```

            # B
            """;
        var result = MarkdownOutline.Parse(md);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Text);
        Assert.Equal("B", result[1].Text);
    }

    [Fact]
    public void Parse_BlockquotedHeading_IsNotTopLevel()
    {
        // '> # H' is a heading inside a blockquote — excluded so the outline stays aligned
        // with the top-level headings rendered in the preview.
        var result = MarkdownOutline.Parse("# Top\n> # Quoted\n## Next");

        Assert.Equal(2, result.Count);
        Assert.Equal("Top", result[0].Text);
        Assert.Equal("Next", result[1].Text);
    }

    [Fact]
    public void Parse_SetextHeading_IsIgnored()
    {
        var result = MarkdownOutline.Parse("Not a heading\n=============\n\n# But this is");

        Assert.Single(result);
        Assert.Equal("But this is", result[0].Text);
    }

    // --- AncestorChain (breadcrumbs, M10) ---

    [Fact]
    public void AncestorChain_NestedHeading_FullChainTopFirst()
    {
        var outline = MarkdownOutline.Parse("# A\n## B\n### C");

        var chain = MarkdownOutline.AncestorChain(outline, 2);

        Assert.Equal(new[] { "A", "B", "C" }, System.Linq.Enumerable.Select(chain, h => h.Text));
    }

    [Fact]
    public void AncestorChain_SkipsSameLevelSiblings()
    {
        var outline = MarkdownOutline.Parse("# A\n## B\n## C");

        var chain = MarkdownOutline.AncestorChain(outline, 2);

        Assert.Equal(new[] { "A", "C" }, System.Linq.Enumerable.Select(chain, h => h.Text));
    }

    [Fact]
    public void AncestorChain_LevelJump_HasNoInvented_Intermediate()
    {
        var outline = MarkdownOutline.Parse("# A\n### C");

        var chain = MarkdownOutline.AncestorChain(outline, 1);

        Assert.Equal(new[] { "A", "C" }, System.Linq.Enumerable.Select(chain, h => h.Text));
    }

    [Fact]
    public void AncestorChain_LaterTopLevel_ResetsTheChain()
    {
        var outline = MarkdownOutline.Parse("# A\n## B\n# C");

        var chain = MarkdownOutline.AncestorChain(outline, 2);

        var only = Assert.Single(chain);
        Assert.Equal("C", only.Text);
    }

    [Fact]
    public void AncestorChain_MinusOneOrOutOfRange_Empty()
    {
        var outline = MarkdownOutline.Parse("# A");

        Assert.Empty(MarkdownOutline.AncestorChain(outline, -1));
        Assert.Empty(MarkdownOutline.AncestorChain(outline, 5));
    }
}
