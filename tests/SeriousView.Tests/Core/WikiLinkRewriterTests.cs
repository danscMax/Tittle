using System;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

/// <summary>The shared wiki pass behind both the viewer (wiki: URLs) and the exporter (.md URLs).
/// The whole point of the consolidation is that the guard set is identical in both formats — these
/// tests pin that, so the export can never again rewrite a token the viewer would leave alone.</summary>
public class WikiLinkRewriterTests
{
    private static string Rewrite(string text, Func<string, string> formatUrl, Func<string, bool>? resolve)
    {
        var lines = LineEndings.NormalizeToLf(text).Split('\n');
        var regions = MarkdownCodeRegions.Scan(lines);
        WikiLinkRewriter.Rewrite(lines, regions, resolve, formatUrl);
        return string.Join("\n", lines);
    }

    private static string Wiki(string text) => Rewrite(text, WikiLink.CreateUrl, _ => true);
    private static string Md(string text) => Rewrite(text, n => $"{Uri.EscapeDataString(n)}.md", _ => true);

    [Fact]
    public void ResolvedName_LinkedInBothFormats()
    {
        Assert.Equal("see [note](wiki:note)", Wiki("see [[note]]"));
        Assert.Equal("see [note](note.md)", Md("see [[note]]"));
    }

    [Fact]
    public void Unresolved_DegradesToPlainText()
    {
        Assert.Equal("note", Rewrite("[[note]]", WikiLink.CreateUrl, _ => false));
        Assert.Equal("note", Rewrite("[[note]]", n => $"{n}.md", null));
    }

    [Fact]
    public void LinkRefDefLine_SkippedInBothFormats()
    {
        const string md = "[ref]: http://example.com [[note]]";
        Assert.Equal(md, Wiki(md));
        Assert.Equal(md, Md(md));
    }

    [Fact]
    public void OverlongLine_SkippedInBothFormats()
    {
        var line = "[[note]] " + new string('x', 10_001);
        Assert.Equal(line, Wiki(line));
        Assert.Equal(line, Md(line));
    }

    [Fact]
    public void FencedAndInlineCode_Skipped()
    {
        Assert.Equal("```\n[[note]]\n```", Wiki("```\n[[note]]\n```"));
        Assert.Equal("a `[[note]]` b", Wiki("a `[[note]]` b"));
    }

    [Fact]
    public void EmptyOrPipeToken_LeftAsAuthored()
    {
        Assert.Equal("[[]]", Md("[[]]"));
        Assert.Equal("[[a|b]]", Md("[[a|b]]"));
    }
}
