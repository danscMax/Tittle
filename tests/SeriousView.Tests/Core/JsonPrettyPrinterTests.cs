using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class JsonPrettyPrinterTests
{
    [Fact]
    public void TryFormat_CompactJson_Indents()
    {
        var result = JsonPrettyPrinter.TryFormat("{\"a\":1,\"b\":[true,null]}");

        Assert.NotNull(result);
        Assert.Contains("\"a\": 1", result);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void TryFormat_PreservesCyrillicAndOrder()
    {
        var result = JsonPrettyPrinter.TryFormat("{\"имя\":\"значение\",\"b\":2}");

        Assert.NotNull(result);
        Assert.Contains("\"имя\": \"значение\"", result); // no \uXXXX escaping
        Assert.True(result!.IndexOf("имя") < result.IndexOf("\"b\""));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("// jsonc comment\n{\"a\":1}")]
    public void TryFormat_InvalidOrCommentedJson_ReturnsNull(string input)
        => Assert.Null(JsonPrettyPrinter.TryFormat(input));
}
