using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

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

    // --- Diagram fences (M12, opt-in) ---

    [Fact]
    public void Transform_DiagramFence_Enabled_BecomesDiagramContainer()
    {
        const string md = "text\n\n```mermaid\ngraph TD;A-->B\n```\n\nmore";
        var result = MarkdownPreprocessor.Transform(md, null, diagramsEnabled: true);

        Assert.Contains("::: diagram", result);
        Assert.DoesNotContain("```mermaid", result);
        // payload is "type|body", both percent-encoded — mermaid has no special chars
        Assert.Contains("mermaid|" + System.Uri.EscapeDataString("graph TD;A-->B"), result);
    }

    [Fact]
    public void Transform_DiagramFence_Disabled_StaysAsCode()
    {
        const string md = "```mermaid\ngraph TD;A-->B\n```";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md, null, diagramsEnabled: false));
        Assert.DoesNotContain("::: diagram", MarkdownPreprocessor.Transform(md, null, false));
    }

    [Fact]
    public void Transform_NonDiagramFence_StaysAsCode_EvenWhenEnabled()
    {
        const string md = "```python\nprint(1)\n```";
        var result = MarkdownPreprocessor.Transform(md, null, diagramsEnabled: true);
        Assert.DoesNotContain("::: diagram", result);
        Assert.Contains("```python", result);
    }

    [Fact]
    public void Transform_UnclosedDiagramFence_LeftAsAuthored()
    {
        const string md = "```dot\ndigraph{}";
        var result = MarkdownPreprocessor.Transform(md, null, diagramsEnabled: true);
        Assert.DoesNotContain("::: diagram", result);
    }

    [Fact]
    public void Transform_DiagramFenceInsideOuterFence_NotConverted()
    {
        // A ```mermaid example shown inside an outer ```` fence is literal text, not a diagram —
        // the whole outer block passes through verbatim (we never peek inside a non-diagram fence).
        const string md = "````\n```mermaid\ngraph TD;A-->B\n```\n````";
        var result = MarkdownPreprocessor.Transform(md, null, diagramsEnabled: true);
        Assert.DoesNotContain("::: diagram", result);
        Assert.Equal(md, result);
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
    public void Wiki_ResolverMemo_IsCaseInsensitive()
    {
        // Q12: File.Exists is case-insensitive on Windows/macOS, so [[Note]] and [[note]] must share
        // one memo entry — a single resolver hit, not two (which could even disagree).
        var calls = new System.Collections.Generic.List<string>();
        var result = MarkdownPreprocessor.Transform("[[Note]] and [[note]]", name =>
        {
            calls.Add(name);
            return true;
        });

        Assert.Single(calls);
        Assert.Equal("[Note](wiki:Note) and [note](wiki:note)", result);
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

    // --- Block math $$…$$ / \[…\] → ::: math containers (M11) ---

    // Math bodies travel PERCENT-ENCODED inside the ::: math container — the renderer's
    // container parser C-unescapes raw bodies (\f of \frac became a form feed), so the math
    // handler Uri-decodes on the other end.
    private static string MathContainer(string latex)
        => "::: math\n" + System.Uri.EscapeDataString(latex) + "\n:::";

    [Fact]
    public void Math_MultilineDollarBlock_BecomesAMathContainer()
    {
        var result = MarkdownPreprocessor.Transform("text\n$$\nE = mc^2\n$$\nafter");

        Assert.Contains(MathContainer("E = mc^2"), result);
        Assert.DoesNotContain("$$", result);
    }

    [Fact]
    public void Math_SingleLineDollarBlock_BecomesAMathContainer()
    {
        var result = MarkdownPreprocessor.Transform(@"$$E = mc^2$$");

        Assert.Contains(MathContainer("E = mc^2"), result);
    }

    [Fact]
    public void Math_BracketBlock_BecomesAMathContainer()
    {
        var multi = MarkdownPreprocessor.Transform("\\[\n\\frac{a}{b}\n\\]");
        var single = MarkdownPreprocessor.Transform(@"\[x^2\]");

        Assert.Contains(MathContainer("\\frac{a}{b}"), multi);
        Assert.Contains(MathContainer("x^2"), single);
    }

    [Fact]
    public void Math_SingleDollar_IsNotADelimiter()
    {
        const string md = "цена $5, а там $10 — не формулы";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md));
    }

    [Fact]
    public void Math_InsideAFence_Unchanged()
    {
        const string md = "```\n$$\nE\n$$\n```";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md));
    }

    [Fact]
    public void Math_UnclosedBlock_LeftAsAuthored()
    {
        const string md = "$$\nE = mc^2\nno closer";
        Assert.Equal(md, MarkdownPreprocessor.Transform(md));
    }

    [Fact]
    public void Math_Body_IsProtectedFromTheInlinePasses()
    {
        var result = MarkdownPreprocessor.Transform("$$\n_x_ + [[a]] - [x] step\n$$", _ => true);

        // The encoded body still contains _x_ / [[a]] verbatim (percent-encoding leaves them),
        // so this also proves the ::: math region guard kept the passes away.
        Assert.Contains(MathContainer("_x_ + [[a]] - [x] step"), result);
    }

    // --- Emoji :name: shortcodes (ported) ---

    [Theory]
    [InlineData("готово :tada: и :rocket:", "готово 🎉 и 🚀")]
    [InlineData("оценка :+1:", "оценка 👍")]
    [InlineData(":fire::fire:", "🔥🔥")]
    public void Emoji_KnownShortcodes_BecomeUnicode(string input, string expected)
        => Assert.Equal(expected, MarkdownPreprocessor.Transform(input));

    [Theory]
    [InlineData(":unknown_code:")]
    [InlineData("time 10:30:45 plain colons")]
    [InlineData("`:tada:` in code")]
    public void Emoji_UnknownOrCodeOrPlainColons_LeftAlone(string input)
        => Assert.Equal(input, MarkdownPreprocessor.Transform(input));

    [Fact]
    public void Emoji_InsideAFence_Unchanged()
        => Assert.Equal("```\n:tada:\n```", MarkdownPreprocessor.Transform("```\n:tada:\n```"));

    // ---- YAML front-matter (ported): a leading --- block → ::: frontmatter container ----

    [Fact]
    public void FrontMatter_AtTheTop_BecomesAContainer()
    {
        var output = MarkdownPreprocessor.Transform("---\ntitle: Заметка\ntags: a, b\n---\n# Body");

        Assert.StartsWith("::: frontmatter\n", output);
        Assert.Contains(":::\n", output);
        Assert.Contains("# Body", output);
        Assert.DoesNotContain("title: Заметка", output); // travels percent-encoded

        // The opaque line decodes back to the raw YAML.
        var encoded = output.Split('\n')[1];
        Assert.Equal("title: Заметка\ntags: a, b", System.Uri.UnescapeDataString(encoded));
    }

    [Fact]
    public void FrontMatter_NotAtLineZero_IsAThematicBreak()
    {
        const string input = "intro\n\n---\nkey: value\n---";
        Assert.Equal(input, MarkdownPreprocessor.Transform(input));
    }

    [Fact]
    public void FrontMatter_Unclosed_LeftAlone()
        => Assert.Equal("---\nkey: value\n# Body",
            MarkdownPreprocessor.Transform("---\nkey: value\n# Body"));

    [Fact]
    public void FrontMatter_EmptyBody_IsNotFrontMatter()
        => Assert.Equal("---\n---\ntext", MarkdownPreprocessor.Transform("---\n---\ntext"));

    [Fact]
    public void FrontMatter_DotsTerminator_Closes()
    {
        var output = MarkdownPreprocessor.Transform("---\ntitle: x\n...\nbody");

        Assert.StartsWith("::: frontmatter\n", output);
        Assert.Contains("body", output);
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

    // --- Bare-URL autolinks (1.2) ---

    [Theory]
    [InlineData("See http://example.com here.", "[http://example.com](http://example.com)")]
    [InlineData("See https://example.com/path?q=1 here.",
        "[https://example.com/path?q=1](https://example.com/path?q=1)")]
    public void Transform_BareUrl_BecomesMarkdownLink(string md, string expected)
        => Assert.Contains(expected, MarkdownPreprocessor.Transform(md));

    [Fact]
    public void Transform_BareUrl_TrailingPunctuation_StaysOutsideTheLink()
    {
        var result = MarkdownPreprocessor.Transform("Visit https://example.com/page, ok.");
        Assert.Contains("[https://example.com/page](https://example.com/page),", result);
        Assert.DoesNotContain("/page,)", result);
    }

    [Fact]
    public void Transform_BareUrl_InInlineCode_IsLeftAlone()
    {
        var result = MarkdownPreprocessor.Transform("Run `curl http://example.com` now.");
        Assert.Contains("`curl http://example.com`", result);
        Assert.DoesNotContain("](http", result);
    }

    [Fact]
    public void Transform_BareUrl_InFencedCode_IsLeftAlone()
    {
        var result = MarkdownPreprocessor.Transform("```\nhttp://example.com\n```");
        Assert.DoesNotContain("](http", result);
    }

    [Fact]
    public void Transform_UrlInsideMarkdownLink_IsNotDoubleWrapped()
    {
        var result = MarkdownPreprocessor.Transform("A [link](https://example.com) text.");
        Assert.Contains("[link](https://example.com)", result);
        Assert.DoesNotContain("[https://example.com]", result);
    }

    [Fact]
    public void Transform_ExistingAutolink_IsNotWrapped()
    {
        var result = MarkdownPreprocessor.Transform("Here <https://example.com> stays.");
        Assert.Contains("<https://example.com>", result);
        Assert.DoesNotContain("](http", result);
    }

    [Fact]
    public void Transform_LinkReferenceDefinition_IsNotWrapped()
    {
        var result = MarkdownPreprocessor.Transform("[ref]: https://example.com");
        Assert.DoesNotContain("](https://example.com)", result);
    }

    [Fact]
    public void Transform_BareUrl_CyrillicAround_DoesNotBreak()
        => Assert.Contains("[https://example.com](https://example.com)",
            MarkdownPreprocessor.Transform("Источник https://example.com здесь."));

    // --- Code-language autodetect (1.3) ---

    [Fact]
    public void Transform_BareFence_JsonBody_GetsJsonLanguage()
    {
        var result = MarkdownPreprocessor.Transform("```\n{ \"name\": \"x\", \"n\": 1 }\n```");
        Assert.Contains("```json", result);
    }

    [Fact]
    public void Transform_BareFence_PythonBody_GetsPythonLanguage()
    {
        var result = MarkdownPreprocessor.Transform("```\ndef f(x):\n    return x\n```");
        Assert.Contains("```python", result);
    }

    [Fact]
    public void Transform_FenceWithLanguage_IsNotRewritten()
    {
        var result = MarkdownPreprocessor.Transform("```js\n{ \"a\": 1 }\n```");
        Assert.Contains("```js", result);
        Assert.DoesNotContain("```json", result);
    }

    [Fact]
    public void Transform_BareFence_AmbiguousBody_StaysBare()
    {
        var result = MarkdownPreprocessor.Transform("```\nhello world\nplain text\n```");
        Assert.DoesNotContain("```json", result);
        Assert.DoesNotContain("```python", result);
        Assert.Contains("```\nhello world", result);
    }

    [Fact]
    public void Transform_PlainTextWithBraces_NotInFence_IsNotTreatedAsCode()
    {
        // No fence → the autodetect pass never runs on it.
        var result = MarkdownPreprocessor.Transform("A line { \"a\": 1 } in prose.");
        Assert.DoesNotContain("```", result);
    }
}
