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

        Assert.Contains("::: tip", result);
        Assert.Contains("Inside.", result);
        Assert.Contains("Outside paragraph.", result);
        // The trailing paragraph must remain outside the container (after the closing :::).
        var openIdx = result.IndexOf("::: tip", System.StringComparison.Ordinal);
        var closeIdx = result.IndexOf(":::", openIdx + 3, System.StringComparison.Ordinal);
        Assert.True(result.IndexOf("Outside paragraph.", System.StringComparison.Ordinal) > closeIdx);
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

    // --- Footnotes ---

    [Fact]
    public void Transform_Footnote_RefBecomesSuperscript_AndSectionAppended()
    {
        var result = MarkdownPreprocessor.Transform("Text[^1].\n\n[^1]: Definition.");

        Assert.Contains("¹", result);                 // reference → superscript
        Assert.Contains("**Сноски**", result);        // footnotes section
        Assert.Contains("1. Definition.", result);    // numbered definition
        Assert.DoesNotContain("[^1]", result);        // original ref + def gone
    }

    [Fact]
    public void Transform_Footnotes_NumberedByReferenceOrder()
    {
        var result = MarkdownPreprocessor.Transform("A[^b] then B[^a].\n\n[^a]: Alpha\n[^b]: Beta");

        Assert.Contains("A¹ then B².", result);   // [^b] referenced first → 1
        Assert.Contains("1. Beta", result);
        Assert.Contains("2. Alpha", result);
    }

    [Fact]
    public void Transform_Footnote_RepeatedReference_SharesNumber()
    {
        var result = MarkdownPreprocessor.Transform("X[^1] Y[^1].\n\n[^1]: One");

        Assert.Equal(2, CountOccurrences(result, "¹"));
        Assert.Contains("1. One", result);
        Assert.DoesNotContain("2.", result);
    }

    [Fact]
    public void Transform_FootnoteReference_WithoutDefinition_LeftAsAuthored()
    {
        const string md = "Dangling[^x] reference.";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }
        return count;
    }
}
