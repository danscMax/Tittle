using System;

namespace SeriousView.Core.Text;

/// <summary>Heuristic binary-vs-text detection (UI-free, testable). A NUL byte in the first
/// few KB means binary — except UTF-16/32 text, which legitimately contains 0x00, so a Unicode
/// BOM short-circuits to "text". The same heuristic git and editors use.</summary>
public static class BinaryContent
{
    private const int ScanWindow = 8192;

    public static bool IsProbablyBinary(byte[] bytes)
    {
        if (HasUnicodeBom(bytes))
            return false;

        var end = Math.Min(bytes.Length, ScanWindow);
        for (var i = 0; i < end; i++)
            if (bytes[i] == 0x00)
                return true;
        return false;
    }

    // UTF-16 LE/BE or UTF-32 LE/BE BOM (UTF-32 LE shares the FF FE prefix with UTF-16 LE).
    private static bool HasUnicodeBom(byte[] b)
        => (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
        || (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
        || (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF);
}
