using SeriousView.Core.Export;
using Xunit;

namespace SeriousView.Tests.Core;

public class HtmlExporterTests
{
    private static string Export(string md, bool dark = true, System.Func<string, bool>? wiki = null)
        => HtmlExporter.Export(md, "doc", dark, wiki);

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("safe <img src=x onerror=\"fetch('https://evil')\"> text")]
    public void Export_RawHtmlInTheDocument_IsEscapedNotExecuted(string hostile)
    {
        // The export lands in a browser (print path) and on the clipboard — a hostile
        // document must never smuggle live markup through it (SEC-001).
        var html = Export($"# Doc\n\n{hostile}");

        // No LIVE tags survive — the markup is escaped to inert visible text.
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;", html);
    }

    [Fact]
    public void Export_FrontMatter_IsConsumedNotRendered()
    {
        var html = Export("---\ntitle: secret-meta\n---\n# Doc");

        Assert.DoesNotContain("secret-meta", html);
        Assert.Contains("<h1", html);
    }

    [Fact]
    public void Export_GfmTable_RendersAsAnHtmlTable()
    {
        var html = Export("| A | B |\n|---|---|\n| 1 | 2 |");

        Assert.Contains("<table>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void Export_TaskList_RendersCheckboxes()
    {
        var html = Export("- [x] done\n- [ ] todo");

        Assert.Contains("checkbox", html);
        Assert.Contains("checked", html);
    }

    [Fact]
    public void Export_Footnotes_Render()
    {
        var html = Export("X[^1]\n\n[^1]: the definition");

        Assert.Contains("the definition", html);
        Assert.Contains("<sup", html);
    }

    [Fact]
    public void Export_CodeContent_IsEscaped()
    {
        var html = Export("```\n<script>alert(1)</script>\n```");

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Export_Title_IsEscaped()
    {
        var html = HtmlExporter.Export("x", "<b>evil</b>", darkTheme: true, wikiLinkResolver: null);

        Assert.DoesNotContain("<title><b>", html);
        Assert.Contains("&lt;b&gt;", html);
    }

    [Fact]
    public void Export_WikiLinks_ResolveToSiblingHrefs()
    {
        var html = Export("see [[my note]] and [[gone]]", wiki: name => name == "my note");

        Assert.Contains("href=\"my%20note.md\"", html);
        Assert.Contains(">my note</a>", html);
        Assert.DoesNotContain("[[gone]]", html);
        Assert.DoesNotContain("gone.md", html); // missing sibling → plain text, no link
    }

    [Fact]
    public void Export_WikiInsideAFence_Unchanged()
    {
        var html = Export("```\n[[note]]\n```", wiki: _ => true);

        Assert.Contains("[[note]]", html);
        Assert.DoesNotContain("note.md", html);
    }

    [Fact]
    public void Export_Theme_SwitchesThePalette()
    {
        var dark = Export("x", dark: true);
        var light = Export("x", dark: false);

        Assert.Contains("<style>", dark);
        Assert.NotEqual(dark, light);
    }

    [Fact]
    public void Export_IsSelfContained()
    {
        var html = Export("# T\n\ntext");

        Assert.DoesNotContain("<link", html);       // no external stylesheets
        Assert.DoesNotContain("<script src", html); // no external scripts
        Assert.StartsWith("<!DOCTYPE html>", html);
    }
}
