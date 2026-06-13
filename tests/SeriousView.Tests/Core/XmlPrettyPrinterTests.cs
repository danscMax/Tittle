using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class XmlPrettyPrinterTests
{
    [Fact]
    public void TryFormat_CompactXml_Indents()
    {
        var result = XmlPrettyPrinter.TryFormat("<root><a>1</a><b><c/></b></root>");

        Assert.NotNull(result);
        Assert.Contains("\n", result);
        Assert.Contains("  <a>1</a>", result); // child indented under root
    }

    [Fact]
    public void TryFormat_PreservesCyrillicContent()
    {
        var result = XmlPrettyPrinter.TryFormat("<имя>значение</имя>");

        Assert.NotNull(result);
        Assert.Contains("<имя>значение</имя>", result);
    }

    [Theory]
    [InlineData("<root><unclosed></root>")]
    [InlineData("not xml at all")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryFormat_InvalidXml_ReturnsNull(string input)
        => Assert.Null(XmlPrettyPrinter.TryFormat(input));
}
