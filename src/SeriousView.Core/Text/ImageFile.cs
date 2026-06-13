using System;
using System.IO;

namespace SeriousView.Core.Text;

/// <summary>Image file recognition (by extension). Kept pure in Core so the loader can route an
/// image to its dedicated viewer kind before the binary-content classifier would reject it as
/// «просмотр недоступен». Raster formats are decoded natively by Avalonia (Skia); <c>.svg</c> is
/// rendered vectorially in the UI layer.</summary>
public static class ImageFile
{
    public static bool IsRasterImageExtension(string? path) => Ext(path) is
        ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".ico";

    public static bool IsSvgExtension(string? path) => Ext(path) == ".svg";

    public static bool IsImageExtension(string? path)
        => IsRasterImageExtension(path) || IsSvgExtension(path);

    private static string Ext(string? path)
        => string.IsNullOrEmpty(path) ? "" : Path.GetExtension(path).ToLowerInvariant();
}
