using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class MarkdownFileTests
{
    [Theory]
    [InlineData(".md")]
    [InlineData(".markdown")]
    [InlineData(".mdown")]
    [InlineData(".mkd")]
    [InlineData(".markdn")]
    [InlineData(".MD")]        // case-insensitive
    [InlineData(".Markdown")]
    public void IsMarkdownExtension_True_ForMarkdownExtensions(string ext)
        => Assert.True(MarkdownFile.IsMarkdownExtension(ext));

    [Theory]
    [InlineData(".cs")]
    [InlineData(".rs")]
    [InlineData(".txt")]
    [InlineData("md")]         // no leading dot
    [InlineData("")]
    [InlineData(null)]
    public void IsMarkdownExtension_False_ForOthers(string? ext)
        => Assert.False(MarkdownFile.IsMarkdownExtension(ext));
}
