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

    // --- Wiki links [[name]] (M10) ---

    private static string TransformWiki(string md, params string[] existing)
        => MarkdownPreprocessor.Transform(md, name => System.Linq.Enumerable.Contains(existing, name));

    [Fact]
    public void Wiki_ResolvedName_BecomesAMarkdownLink()
    {
        Assert.Equal("see [note](wiki:note)", TransformWiki("see [[note]]", "note"));
    }

    [Fact]
    public void Wiki_UnresolvedOrInvalid_StripsToPlainText()
    {
        Assert.Equal("see note", TransformWiki("see [[note]]"));
        Assert.Equal("see ../evil", TransformWiki("see [[../evil]]", "../evil"));
        Assert.Equal("see ^x", TransformWiki("see [[^x]]", "^x"));
    }

    [Fact]
    public void Wiki_NullResolver_StripsToPlainText()
    {
        Assert.Equal("see note", MarkdownPreprocessor.Transform("see [[note]]", null));
    }

    [Fact]
    public void Wiki_MultipleAndAdjacentTokens_OnOneLine()
    {
        Assert.Equal("[a](wiki:a) and [b](wiki:b)", TransformWiki("[[a]] and [[b]]", "a", "b"));
        Assert.Equal("[a](wiki:a)[b](wiki:b)", TransformWiki("[[a]][[b]]", "a", "b"));
    }

    [Fact]
    public void Wiki_PipeOrEmpty_LeftAsAuthored()
    {
        Assert.Equal("[[a|b]]", TransformWiki("[[a|b]]", "a"));
        Assert.Equal("[[]]", TransformWiki("[[]]"));
        Assert.Equal("[[  ]]", TransformWiki("[[  ]]"));
    }

    [Fact]
    public void Wiki_NameIsTrimmed()
    {
        Assert.Equal("[note](wiki:note)", TransformWiki("[[ note ]]", "note"));
    }

    [Fact]
    public void Wiki_EncodesSpacesAndCyrillic()
    {
        Assert.Equal("[doc with spaces](wiki:doc%20with%20spaces)",
            TransformWiki("[[doc with spaces]]", "doc with spaces"));
        Assert.StartsWith("[Заметка](wiki:%D0", TransformWiki("[[Заметка]]", "Заметка"));
    }

    [Fact]
    public void Wiki_SkippedInsideFencesAndInlineCode()
    {
        Assert.Equal("```\n[[note]]\n```", TransformWiki("```\n[[note]]\n```", "note"));
        Assert.Equal("a `[[note]]` b", TransformWiki("a `[[note]]` b", "note"));
    }

    [Fact]
    public void Wiki_WorksInsideAdmonitionBodies()
    {
        var result = TransformWiki("> [!NOTE]\n> see [[note]]", "note");

        Assert.Contains("see [note](wiki:note)", result);
    }

    [Fact]
    public void Wiki_DoesNotTouchFootnotes_AndViceVersa()
    {
        var result = TransformWiki("X[^1] and [[note]]\n\n[^1]: def", "note");

        Assert.Contains("[note](wiki:note)", result);
        Assert.Contains("¹", result); // the footnote pass still ran
    }

    [Fact]
    public void Wiki_SkipsLinkReferenceDefinitionLines()
    {
        const string md = "[ref]: http://example.com [[note]]";
        Assert.Equal(md, TransformWiki(md, "note"));
    }

    [Fact]
    public void Wiki_MemoizesTheResolver()
    {
        var calls = 0;
        MarkdownPreprocessor.Transform("[[a]] then [[a]] again", _ =>
        {
            calls++;
            return true;
        });

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Wiki_OverlongLine_IsLeftAlone()
    {
        var line = "[[note]] " + new string('x', 10_001);
        Assert.Equal(line, TransformWiki(line, "note"));
    }

    // --- _underscore_ italics (M10, display-only) ---

    [Theory]
    [InlineData("_x_", "*x*")]
    [InlineData("a _b_ c", "a *b* c")]
    [InlineData("_em_,", "*em*,")]
    [InlineData("(_em_)", "(*em*)")]
    [InlineData("**_x_**", "***x***")]
    [InlineData("[_x_](u)", "[*x*](u)")]
    [InlineData("_x_ and _y_", "*x* and *y*")]
    public void Underscore_WordEmphasis_BecomesAsterisks(string input, string expected)
        => Assert.Equal(expected, MarkdownPreprocessor.Transform(input));

    [Theory]
    [InlineData("snake_case_name")]
    [InlineData("a_b_c")]
    [InlineData("слово_х_")]
    [InlineData("__keep__")]
    [InlineData("___x___")]
    [InlineData("_x__")]
    [InlineData("_ a_")]
    [InlineData("_a _")]
    [InlineData("[a](http://x.com/_y_/z)")]
    [InlineData("<http://ex.com/_path_>")]
    [InlineData("[ref]: http://x/_y_")]
    public void Underscore_IntrawordCodeUrlsAndDoubles_AreLeftAlone(string input)
        => Assert.Equal(input, MarkdownPreprocessor.Transform(input));

    [Fact]
    public void Underscore_SkippedInsideFencesAndInlineCode()
    {
        Assert.Equal("```\n_x_\n```", MarkdownPreprocessor.Transform("```\n_x_\n```"));
        Assert.Equal("a `_x_` b", MarkdownPreprocessor.Transform("a `_x_` b"));
    }

    [Fact]
    public void Underscore_AppliesInsideAdmonitionBodies()
    {
        var result = MarkdownPreprocessor.Transform("> [!NOTE]\n> важное _слово_ тут");

        Assert.Contains("важное *слово* тут", result);
    }

    [Fact]
    public void Underscore_ProtectsTheWikiPassOutput()
    {
        var result = MarkdownPreprocessor.Transform("see [[my_note]]", _ => true);

        Assert.Equal("see [my_note](wiki:my_note)", result); // dest masked, text intraword
    }

    // --- Fence-guard retrofit: the legacy passes must not transform inside ``` fences ---

    [Fact]
    public void Fence_TaskListInside_Unchanged_OutsideStillConverts()
    {
        var result = MarkdownPreprocessor.Transform("- [x] real\n\n```\n- [x] keep\n- [ ] also\n```");

        Assert.StartsWith("- ☑ real", result); // the real item converts (list marker kept)
        Assert.Contains("- [x] keep", result);
        Assert.Contains("- [ ] also", result);
    }

    [Fact]
    public void Fence_FootnotesInside_Unchanged_OutsideStillConvert()
    {
        var result = MarkdownPreprocessor.Transform(
            "A[^2]\n\n```\nX[^1] text\n[^1]: inner def\n```\n\n[^2]: real");

        Assert.Contains("A¹", result);              // the real reference resolved as #1
        Assert.Contains("1. real", result);         // the real definition listed
        Assert.Contains("X[^1] text", result);      // fenced reference untouched
        Assert.Contains("[^1]: inner def", result); // fenced definition stays in the body...
        Assert.DoesNotContain("2.", result);        // ...and never registers as a footnote
    }

    [Fact]
    public void Fence_AlertInside_DoesNotBecomeAnAdmonition()
    {
        var result = MarkdownPreprocessor.Transform("```\n> [!NOTE]\n> body\n```");

        Assert.DoesNotContain("::: note", result);
        Assert.Contains("[!NOTE]", result);
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
