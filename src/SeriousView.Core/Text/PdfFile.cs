using System;
using System.IO;

namespace SeriousView.Core.Text;

/// <summary>PDF file recognition (by extension). Kept pure in Core so the loader can route a
/// <c>.pdf</c> to its dedicated viewer kind before the binary-content classifier would otherwise
/// reject it as «просмотр недоступен». The actual page rendering (PDFium via SkiaSharp) lives in
/// the UI layer.</summary>
public static class PdfFile
{
    public static bool IsPdfExtension(string? path)
        => !string.IsNullOrEmpty(path)
           && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
}
