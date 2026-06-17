using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class TextMetricsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("one line", 1)]
    [InlineData("a\nb", 2)]
    [InlineData("a\nb\nc", 3)]
    [InlineData("a\nb\n", 3)] // trailing newline starts a 3rd (empty) line
    public void LineCount_CountsLines(string text, int expected)
        => Assert.Equal(expected, TextMetrics.LineCount(text));

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("a\nb", 3)]
    [InlineData(null, 0)] // null-safe, matching LineCount
    public void CharCount_CountsChars(string? text, int expected)
        => Assert.Equal(expected, TextMetrics.CharCount(text));
}
