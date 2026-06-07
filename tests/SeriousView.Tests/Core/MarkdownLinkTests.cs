using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class MarkdownLinkTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?q=1#frag")]
    [InlineData("HTTPS://EXAMPLE.COM")]     // Uri lower-cases the scheme
    [InlineData("mailto:user@example.com")]
    public void IsSafe_True_ForWebAndMail(string url)
        => Assert.True(MarkdownLink.IsSafe(url));

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    [InlineData("vbscript:msgbox")]
    [InlineData("./relative/path.md")]      // relative → not absolute → unsafe
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsSafe_False_ForEverythingElse(string? url)
        => Assert.False(MarkdownLink.IsSafe(url));
}
