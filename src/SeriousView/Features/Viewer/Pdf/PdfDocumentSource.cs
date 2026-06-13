using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;

// CA1416: PDFtoImage's Conversion methods are annotated for Windows/Linux/macOS/Android. SeriousView
// ships only on Windows/Linux/macOS — all supported — and a missing native is already handled by the
// graceful fallback below, so the platform-compatibility warning is a false positive here.
#pragma warning disable CA1416

namespace SeriousView.Features.Viewer.Pdf;

/// <summary>Renders PDF pages to Avalonia bitmaps via PDFtoImage (PDFium + SkiaSharp). PDFium is a
/// single global native engine and is NOT thread-safe, so every native call is serialized through
/// one process-wide gate and marshalled off the UI thread. Page aspect ratios are read once up
/// front so the view can size (and virtualize) page placeholders before any bitmap is rendered.
/// Lives in the UI layer — Core stays free of the SkiaSharp/native dependency.</summary>
public sealed class PdfDocumentSource
{
    // PDFium is one global native engine — serialize ALL calls across every open document.
    private static readonly SemaphoreSlim NativeGate = new(1, 1);

    private const double FallbackAspect = 1.4142; // A4 portrait (h/w) when a page size is unknown

    // The PDF bytes, held for the document's lifetime so lazy per-page renders need no open file
    // handle. (PDFtoImage's string overload is base64, NOT a path — byte[] is the clean route.)
    private readonly byte[] _bytes;

    public int PageCount { get; }

    /// <summary>Per-page aspect ratio (height / width), for placeholder sizing.</summary>
    public IReadOnlyList<double> AspectRatios { get; }

    /// <summary>Per-page natural width in CSS pixels (PDF points × 96/72), for the «100%» view.</summary>
    public IReadOnlyList<double> NaturalWidths { get; }

    private PdfDocumentSource(
        byte[] bytes, int pageCount, IReadOnlyList<double> aspectRatios, IReadOnlyList<double> naturalWidths)
    {
        _bytes = bytes;
        PageCount = pageCount;
        AspectRatios = aspectRatios;
        NaturalWidths = naturalWidths;
    }

    /// <summary>Open a PDF and read its page metadata. Returns null when PDFium can't load it
    /// (native missing for this RID, or a corrupt/encrypted file) — the caller then falls back to
    /// "open externally". Never throws.</summary>
    public static PdfDocumentSource? TryOpen(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            NativeGate.Wait();
            try
            {
                var count = Conversion.GetPageCount(bytes);
                if (count <= 0)
                    return null;

                var sizes = Conversion.GetPageSizes(bytes);
                var aspect = new double[count];
                var natural = new double[count];
                for (var i = 0; i < count; i++)
                {
                    var s = i < sizes.Count ? sizes[i] : default;
                    aspect[i] = s.Width > 0 ? (double)s.Height / s.Width : FallbackAspect;
                    natural[i] = s.Width > 0 ? s.Width * 96.0 / 72.0 : 612 * 96.0 / 72.0; // pt → px (A4-ish fallback)
                }

                return new PdfDocumentSource(bytes, count, aspect, natural);
            }
            finally { NativeGate.Release(); }
        }
        catch
        {
            return null; // native missing / unreadable → graceful fallback
        }
    }

    /// <summary>Render one page to an Avalonia bitmap at the given target pixel width (off the UI
    /// thread, serialized). Returns null on cancellation or a render failure.</summary>
    public async Task<Bitmap?> RenderPageAsync(int page, int pixelWidth, CancellationToken ct)
    {
        if (page < 0 || page >= PageCount || pixelWidth <= 0)
            return null;

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            NativeGate.Wait(ct);
            try
            {
                var options = new RenderOptions
                {
                    Width = pixelWidth,
                    WithAspectRatio = true,
                    WithAnnotations = true,
                    WithFormFill = true,
                    BackgroundColor = SKColors.White,
                };

                using var sk = Conversion.ToImage(_bytes, page: page, options: options);
                using var data = sk.Encode(SKEncodedImageFormat.Png, 90);
                using var ms = new MemoryStream(data.ToArray());
                return new Bitmap(ms);
            }
            finally { NativeGate.Release(); }
        }, ct).ConfigureAwait(false);
    }
}
