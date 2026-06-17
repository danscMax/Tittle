using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Tittle.Features.Viewer.Pdf;
using SkiaSharp;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>End-to-end PDF rendering smoke: generate a one-page PDF with SkiaSharp, then open and
/// render it through <see cref="PdfDocumentSource"/> (PDFium). Deterministic — both native engines
/// ship per-RID via bblanchon.PDFium / SkiaSharp — and proves the native pdfium actually loads on
/// the test platform (the one thing the headless view tests can't cover).</summary>
public class PdfRenderingTests
{
    private static string WriteOnePagePdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sv_{Guid.NewGuid():N}.pdf");
        using var stream = File.OpenWrite(path);
        using var doc = SKDocument.CreatePdf(stream);
        var canvas = doc.BeginPage(300, 200);
        canvas!.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.Black, TextSize = 24, IsAntialias = true };
        canvas.DrawText("Tittle PDF", 30, 100, paint);
        doc.EndPage();
        doc.Close();
        return path;
    }

    [AvaloniaFact] // needs the headless platform for the Avalonia Bitmap decode
    public async Task TryOpen_AndRenderPage_ProducesABitmap()
    {
        var path = WriteOnePagePdf();
        try
        {
            // TryOpen reading page metadata proves the native pdfium engine loaded for this RID.
            var source = PdfDocumentSource.TryOpen(path);
            Assert.NotNull(source);
            Assert.Equal(1, source!.PageCount);
            Assert.True(source.AspectRatios[0] > 0);

            // Rendering returns a decoded bitmap (exact PixelSize is a headless-platform detail, so
            // assert only that PDFium → PNG → Avalonia Bitmap succeeded).
            var bitmap = await source.RenderPageAsync(0, pixelWidth: 600, CancellationToken.None);
            Assert.NotNull(bitmap);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryOpen_NonPdfBytes_ReturnsNull_NoThrow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sv_{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "this is not a real pdf");
        try
        {
            Assert.Null(PdfDocumentSource.TryOpen(path)); // graceful fallback, never throws
        }
        finally { File.Delete(path); }
    }
}
