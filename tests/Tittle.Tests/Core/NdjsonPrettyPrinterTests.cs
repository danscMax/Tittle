using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class NdjsonPrettyPrinterTests
{
    [Fact]
    public void TryFormat_MultipleLines_IndentsEachAndSeparatesWithBlankLine()
    {
        var result = NdjsonPrettyPrinter.TryFormat("{\"a\":1}\n{\"b\":2}");

        Assert.NotNull(result);
        Assert.Contains("\"a\": 1", result);
        Assert.Contains("\"b\": 2", result);
        Assert.Contains("\n\n", result); // records separated by a blank line
    }

    [Fact]
    public void TryFormat_BestEffort_KeepsUnparseableLinesVerbatim()
    {
        var result = NdjsonPrettyPrinter.TryFormat("{\"a\":1}\nnot json\n{\"b\":2}");

        Assert.NotNull(result);
        Assert.Contains("\"a\": 1", result);
        Assert.Contains("not json", result); // kept as-is
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nope\nstill nope")]
    public void TryFormat_NothingParses_ReturnsNull(string input)
        => Assert.Null(NdjsonPrettyPrinter.TryFormat(input));
}
