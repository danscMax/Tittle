using System;
using System.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Tittle.Core.Documents;
using Tittle.Features.Shell;
using SkiaSharp;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>Image-file loading smoke: a raster file decodes to an Avalonia <see cref="Bitmap"/> and
/// an .svg loads as an <see cref="SvgImage"/> — both as the <c>IImage</c> the view binds. Needs the
/// headless platform for the decoders.</summary>
public class ImageLoadingTests
{
    [AvaloniaFact]
    public void RasterFile_LoadsAsBitmap()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sv_{Guid.NewGuid():N}.png");
        using (var bmp = new SKBitmap(24, 16))
        using (var img = SKImage.FromBitmap(bmp))
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
        using (var fs = File.OpenWrite(path))
            data.SaveTo(fs);
        try
        {
            var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Image(new FileInfo(path).Length), path);
            Assert.IsType<Bitmap>(vm.ImageSource);
        }
        finally { File.Delete(path); }
    }

    [AvaloniaFact]
    public void SvgFile_LoadsAsSvgImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sv_{Guid.NewGuid():N}.svg");
        File.WriteAllText(path,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"32\" height=\"32\"><rect width=\"32\" height=\"32\" fill=\"red\"/></svg>");
        try
        {
            var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Image(new FileInfo(path).Length), path);
            Assert.IsType<SvgImage>(vm.ImageSource);
        }
        finally { File.Delete(path); }
    }
}
