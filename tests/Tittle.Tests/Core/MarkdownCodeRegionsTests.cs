using System.Text.RegularExpressions;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class MarkdownCodeRegionsTests
{
    private static MarkdownCodeRegions Scan(params string[] lines) => MarkdownCodeRegions.Scan(lines);

    private static bool[] Flags(MarkdownCodeRegions regions, int count)
    {
        var flags = new bool[count];
        for (var i = 0; i < count; i++)
            flags[i] = regions.IsFencedLine(i);
        return flags;
    }

    // --- Scan: fenced blocks ---

    [Fact]
    public void Scan_BacktickFence_FlagsDelimitersAndBody()
    {
        var r = Scan("text", "```", "code", "```", "after");

        Assert.Equal(new[] { false, true, true, true, false }, Flags(r, 5));
    }

    [Fact]
    public void Scan_TildeFence_WithInfoString()
    {
        var r = Scan("~~~ cs info", "code", "~~~", "after");

        Assert.Equal(new[] { true, true, true, false }, Flags(r, 4));
    }

    [Fact]
    public void Scan_BacktickInfoStringWithBacktick_IsNotAFence()
    {
        // CommonMark: a backtick fence's info string may not contain a backtick.
        var r = Scan("``` a`b", "still text");

        Assert.Equal(new[] { false, false }, Flags(r, 2));
    }

    [Fact]
    public void Scan_CloserMustMatchCharAndLength()
    {
        // ```` opened with four backticks: ``` does not close it; ~~~ never closes a ` fence.
        var r = Scan("````", "code", "```", "~~~", "````", "after");

        Assert.Equal(new[] { true, true, true, true, true, false }, Flags(r, 6));
    }

    [Fact]
    public void Scan_CloserWithTrailingText_DoesNotClose()
    {
        var r = Scan("```", "code", "``` not a closer", "```", "after");

        Assert.Equal(new[] { true, true, true, true, false }, Flags(r, 5));
    }

    [Fact]
    public void Scan_UnclosedFence_RunsToTheEnd()
    {
        var r = Scan("```", "code", "more");

        Assert.Equal(new[] { true, true, true }, Flags(r, 3));
    }

    [Fact]
    public void Scan_IndentLimits()
    {
        // Up to 3 leading spaces is still a fence; 4 spaces is an indented code line, not a fence.
        var r = Scan("   ```", "code", "   ```", "    ```", "text");

        Assert.Equal(new[] { true, true, true, false, false }, Flags(r, 5));
    }

    [Fact]
    public void IsFencedLine_OutOfRange_False()
    {
        var r = Scan("```", "x", "```");

        Assert.False(r.IsFencedLine(-1));
        Assert.False(r.IsFencedLine(99));
    }

    [Fact]
    public void Scan_MathContainer_IsAProtectedRegion()
    {
        // ::: math bodies carry raw LaTeX — the text passes must leave them alone (M11).
        var r = Scan("text", "::: math", "E = mc^2", ":::", "after");

        Assert.Equal(new[] { false, true, true, true, false }, Flags(r, 5));
    }

    [Fact]
    public void Scan_OtherContainers_AreNotProtected()
    {
        var r = Scan("::: note", "body", ":::");

        Assert.Equal(new[] { false, false, false }, Flags(r, 3));
    }

    // --- ReplaceOutsideCode: inline-span masking ---

    private static readonly Regex Foo = new("foo");

    private static string ReplaceFoo(string line, params Regex[] masks)
        => MarkdownCodeRegions.ReplaceOutsideCode(line, Foo, _ => "BAR", masks);

    [Fact]
    public void Replace_SkipsInlineCodeSpans()
    {
        Assert.Equal("BAR `foo` BAR", ReplaceFoo("foo `foo` foo"));
    }

    [Fact]
    public void Replace_HandlesLongerBacktickRuns()
    {
        Assert.Equal("``a `foo` b`` BAR", ReplaceFoo("``a `foo` b`` foo"));
    }

    [Fact]
    public void Replace_MatchStraddlingASpanBoundary_IsLeftAlone()
    {
        // The match "c`d" overlaps the end of the `b c` span → untouched.
        var line = "a `b c`d";
        var result = MarkdownCodeRegions.ReplaceOutsideCode(line, new Regex("c`d"), _ => "X");

        Assert.Equal(line, result);
    }

    [Fact]
    public void Replace_UnmatchedBacktick_DoesNotMask()
    {
        Assert.Equal("a ` b BAR", ReplaceFoo("a ` b foo"));
    }

    [Fact]
    public void Replace_HonorsExtraMasks()
    {
        var bracket = new Regex(@"\[[^\]]*\]");

        Assert.Equal("BAR [skip foo skip] BAR", ReplaceFoo("foo [skip foo skip] foo", bracket));
    }

    // --- Fence primitive (the single source shared with the preprocessor diagram/language passes) ---

    [Fact]
    public void TryMatchFenceOpen_CapturesCharLengthAndRawInfo()
    {
        Assert.True(MarkdownCodeRegions.TryMatchFenceOpen("```python {.line-numbers}", out var m));
        Assert.Equal('`', m.Char);
        Assert.Equal(3, m.Length);
        Assert.Equal("python {.line-numbers}", m.Info); // permissive: an attribute info string is still a fence
    }

    [Fact]
    public void TryMatchFenceOpen_TabIndent_IsNotAFence()
    {
        // CommonMark: a leading tab is 4 columns of indent → not a code fence (spaces-only, ≤3).
        Assert.False(MarkdownCodeRegions.TryMatchFenceOpen("\t```mermaid", out _));
    }

    [Fact]
    public void TryMatchFenceOpen_BacktickInfoWithBacktick_IsNotAFence()
    {
        Assert.False(MarkdownCodeRegions.TryMatchFenceOpen("``` a`b", out _));
    }

    [Theory]
    [InlineData("```", '`', 3, true)]        // bare closer, exact length
    [InlineData("````", '`', 3, true)]       // a longer run closes a shorter opener
    [InlineData("   ```  ", '`', 3, true)]   // ≤3 spaces + trailing whitespace
    [InlineData("``", '`', 3, false)]        // too short
    [InlineData("```json", '`', 3, false)]   // a closer carries no info
    [InlineData("~~~", '`', 3, false)]       // wrong fence char
    [InlineData("    ```", '`', 3, false)]   // 4 spaces is over-indented
    public void IsFenceClose_MatchesCharLengthAndBareness(string line, char ch, int min, bool expected)
        => Assert.Equal(expected, MarkdownCodeRegions.IsFenceClose(line, ch, min));

    [Theory]
    [InlineData("python", "python")]
    [InlineData("  dot  ", "dot")]
    [InlineData("c++", "c++")]
    [InlineData("", "")]                          // a bare fence has an empty (but clean) lang
    [InlineData("python {.line-numbers}", null)]  // attributes → no clean language
    [InlineData("two words", null)]
    public void FenceLang_ReturnsCleanTokenOrNull(string info, string? expected)
        => Assert.Equal(expected, MarkdownCodeRegions.FenceLang(info));
}
