using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class ImageFileTests
{
    [Theory]
    [InlineData("/a/p.png", true, false)]
    [InlineData("/a/p.JPG", true, false)]
    [InlineData("/a/p.jpeg", true, false)]
    [InlineData("/a/p.gif", true, false)]
    [InlineData("/a/p.bmp", true, false)]
    [InlineData("/a/p.webp", true, false)]
    [InlineData("/a/p.ico", true, false)]
    [InlineData("/a/p.svg", false, true)]
    [InlineData("/a/p.SVG", false, true)]
    [InlineData("/a/p.txt", false, false)]
    [InlineData("/a/p", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    public void Detection_ClassifiesRasterAndSvg(string? path, bool raster, bool svg)
    {
        Assert.Equal(raster, ImageFile.IsRasterImageExtension(path));
        Assert.Equal(svg, ImageFile.IsSvgExtension(path));
        Assert.Equal(raster || svg, ImageFile.IsImageExtension(path));
    }
}
