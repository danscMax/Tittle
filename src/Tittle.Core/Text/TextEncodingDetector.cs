using System.Text;
using System.Text.Unicode;

namespace Tittle.Core.Text;

/// <summary>
/// Detects a text file's encoding from its bytes and decodes it (UI-free, testable).
/// Strategy: BOM (UTF-8/16/32) → strict UTF-8 → Windows-1251 fallback. A wrong guess on a rare
/// charset is non-fatal in a viewer (the detected name is surfaced to the user). The code-pages
/// provider is registered so Windows-1251 is available even under InvariantGlobalization
/// (which affects cultures, not encodings).
/// </summary>
public static class TextEncodingDetector
{
    static TextEncodingDetector()
        => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Decode <paramref name="bytes"/> to text and report the chosen encoding's display
    /// name. Any leading BOM is dropped from the result.</summary>
    public static (string Text, string EncodingName) Decode(byte[] bytes)
    {
        var (encoding, name, bomLength) = Pick(bytes);
        return (encoding.GetString(bytes, bomLength, bytes.Length - bomLength), name);
    }

    private static (Encoding Encoding, string Name, int BomLength) Pick(byte[] b)
    {
        // BOM signatures — check UTF-32 before UTF-16 (they share the FF FE / 00 00 prefixes).
        if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF)
            return (new UTF32Encoding(bigEndian: true, byteOrderMark: false), "UTF-32 BE", 4);
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00)
            return (new UTF32Encoding(bigEndian: false, byteOrderMark: false), "UTF-32 LE", 4);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            return (Encoding.UTF8, "UTF-8", 3);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            return (Encoding.BigEndianUnicode, "UTF-16 BE", 2);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            return (Encoding.Unicode, "UTF-16 LE", 2);

        // No BOM: prefer strict UTF-8, otherwise fall back to Windows-1251.
        return IsValidUtf8(b)
            ? (Encoding.UTF8, "UTF-8", 0)
            : (Encoding.GetEncoding(1251), "Windows-1251", 0);
    }

    // Validate UTF-8 well-formedness without materializing a (whole-file) throwaway string — Decode
    // then does the single real decode. Same verdict as a strict throwing decoder, no allocation.
    private static bool IsValidUtf8(byte[] bytes) => Utf8.IsValid(bytes);
}
