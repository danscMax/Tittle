using System.Collections.Generic;

namespace Tittle.Features.Viewer.Pdf;

/// <summary>Presentation model for a PDF document's pages (the PDF counterpart of
/// <c>CsvTableViewModel</c>): a fixed list of lazily-rendered page view-models. Null when the
/// document can't be opened (the view shows the "open externally" fallback instead).</summary>
public sealed class PdfPagesViewModel
{
    public PdfDocumentSource Source { get; }

    public IReadOnlyList<PdfPageViewModel> Pages { get; }

    public int PageCount => Source.PageCount;

    private PdfPagesViewModel(PdfDocumentSource source)
    {
        Source = source;
        var pages = new PdfPageViewModel[source.PageCount];
        for (var i = 0; i < pages.Length; i++)
            pages[i] = new PdfPageViewModel(source, i);
        Pages = pages;
    }

    /// <summary>Open the PDF, or return null if PDFium can't render it on this platform/file.</summary>
    public static PdfPagesViewModel? TryOpen(string path)
        => PdfDocumentSource.TryOpen(path) is { } source ? new PdfPagesViewModel(source) : null;
}
