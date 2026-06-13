using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class PdfFileTests
{
    [Theory]
    [InlineData("/docs/report.pdf", true)]
    [InlineData("/docs/REPORT.PDF", true)]
    [InlineData("C:/a/b.PdF", true)]
    [InlineData("/docs/report.txt", false)]
    [InlineData("/docs/report", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPdfExtension_MatchesOnlyPdf(string? path, bool expected)
        => Assert.Equal(expected, PdfFile.IsPdfExtension(path));
}
