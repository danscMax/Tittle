using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class MarkdownPreprocessorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Transform_EmptyInput_ReturnsEmpty(string? input)
        => Assert.Equal("", MarkdownPreprocessor.Transform(input));

    [Fact]
    public void Transform_PlainMarkdown_RoundTrips()
    {
        const string md = "# Title\n\nA paragraph with **bold** and a [link](https://x.com).";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md));
    }

    // --- Admonitions ---

    [Fact]
    public void Transform_Alert_BecomesAdmonitionContainer()
    {
        var result = MarkdownPreprocessor.Transform("> [!NOTE]\n> First.\n> Second.");

        Assert.Contains("::: note", result);
        Assert.Contains("First.", result);
        Assert.Contains("Second.", result);
        Assert.Contains(":::", result);
        Assert.DoesNotContain("[!NOTE]", result);
    }

    [Theory]
    [InlineData("NOTE", "::: note")]
    [InlineData("tip", "::: tip")]
    [InlineData("Important", "::: important")]
    [InlineData("WARNING", "::: warning")]
    [InlineData("caution", "::: caution")]
    public void Transform_Alert_TypeIsCaseInsensitiveAndLowercased(string marker, string expected)
    {
        var result = MarkdownPreprocessor.Transform($"> [!{marker}]\n> Body.");
        Assert.Contains(expected, result);
    }

    [Fact]
    public void Transform_PlainBlockquote_IsLeftUntouched()
    {
        const string md = "> just a normal quote\n> second line";
        var result = MarkdownPreprocessor.Transform(md);

        Assert.DoesNotContain(":::", result);
        Assert.Contains("> just a normal quote", result);
    }

    [Fact]
    public void Transform_Alert_StopsAtFirstNonQuotedLine()
    {
        var result = MarkdownPreprocessor.Transform("> [!TIP]\n> Inside.\n\nOutside paragraph.");

        Assert.Contains("::: admonition-tip", result);
        Assert.Contains("Inside.", result);
        Assert.Contains("Outside paragraph.", result);
        // The trailing paragraph must remain outside the container.
        var closeIdx = result.IndexOf(":::", result.IndexOf("admonition-tip"));
        Assert.True(result.IndexOf("Outside paragraph.") > closeIdx);
    }

    // --- Task lists ---

    [Theory]
    [InlineData("- [x] done", "- ☑ done")]
    [InlineData("- [X] done", "- ☑ done")]
    [InlineData("- [ ] todo", "- ☐ todo")]
    [InlineData("* [x] star", "* ☑ star")]
    [InlineData("+ [ ] plus", "+ ☐ plus")]
    [InlineData("  - [x] indented", "  - ☑ indented")]
    public void Transform_TaskItem_BecomesGlyph(string input, string expected)
        => Assert.Equal(expected, MarkdownPreprocessor.Transform(input));

    [Theory]
    [InlineData("- regular item")]
    [InlineData("text [x] not a task")]
    [InlineData("- [z] not a checkbox")]
    public void Transform_NonTaskLine_Unchanged(string input)
        => Assert.Equal(input, MarkdownPreprocessor.Transform(input));

    [Fact]
    public void Transform_TaskListInsideAlert_BothApplied()
    {
        var result = MarkdownPreprocessor.Transform("> [!NOTE]\n> - [x] nested done\n> - [ ] nested todo");

        Assert.Contains("::: note", result);
        Assert.Contains("- ☑ nested done", result);
        Assert.Contains("- ☐ nested todo", result);
    }
}
