using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class LineEndingsTests
{
    [Theory]
    [InlineData("a\nb", "LF")]
    [InlineData("a\r\nb", "CRLF")]
    [InlineData("a\rb", "CR")]
    [InlineData("a\r\nb\nc", "Mixed")]
    [InlineData("no breaks", "")]
    [InlineData("", "")]
    public void Detect_DominantLineEnding(string text, string expected)
        => Assert.Equal(expected, LineEndings.Detect(text));

    [Theory]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("a\r\nb\rc\nd", "a\nb\nc\nd")]
    public void NormalizeToLf_CollapsesToLf(string input, string expected)
        => Assert.Equal(expected, LineEndings.NormalizeToLf(input));
}
