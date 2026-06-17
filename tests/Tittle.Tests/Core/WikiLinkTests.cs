using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class WikiLinkTests
{
    [Theory]
    [InlineData("note")]
    [InlineData("doc with spaces")]
    [InlineData("Заметка")]
    [InlineData("a.b")]
    public void IsValidName_AcceptsPlainNames(string name)
        => Assert.True(WikiLink.IsValidName(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("C:")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("..")]
    [InlineData("a..b")]
    [InlineData(".")]
    [InlineData("^note")]
    [InlineData("a\tb")]
    public void IsValidName_RejectsSeparatorsTraversalAndControl(string name)
        => Assert.False(WikiLink.IsValidName(name));

    [Fact]
    public void CreateUrl_PercentEncodes_AndRoundTrips()
    {
        var url = WikiLink.CreateUrl("doc with spaces");

        Assert.Equal("wiki:doc%20with%20spaces", url);
        Assert.True(WikiLink.TryGetName(url, out var name));
        Assert.Equal("doc with spaces", name);
    }

    [Fact]
    public void CreateUrl_Cyrillic_RoundTrips()
    {
        var url = WikiLink.CreateUrl("Заметка");

        Assert.True(WikiLink.TryGetName(url, out var name));
        Assert.Equal("Заметка", name);
    }

    [Fact]
    public void TryGetName_PrefixIsCaseInsensitive()
    {
        Assert.True(WikiLink.TryGetName("WIKI:note", out var name));
        Assert.Equal("note", name);
    }

    [Theory]
    [InlineData("wiki:%2e%2e%2fpasswd")] // decoded "../passwd" → traversal
    [InlineData("wiki:a%2Fb")]           // decoded "a/b" → separator
    [InlineData("wiki:")]
    [InlineData("https://example.com")]
    [InlineData(null)]
    public void TryGetName_RejectsTraversalAndForeignSchemes(string? url)
        => Assert.False(WikiLink.TryGetName(url, out _));

    [Fact]
    public void IsWikiUrl_ChecksThePrefixOnly()
    {
        Assert.True(WikiLink.IsWikiUrl("wiki:x"));
        Assert.True(WikiLink.IsWikiUrl("Wiki:x"));
        Assert.False(WikiLink.IsWikiUrl("wikipedia:x"));
        Assert.False(WikiLink.IsWikiUrl(null));
    }
}
