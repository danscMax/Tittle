using System.Text;

namespace SeriousView.Core.Text;

/// <summary>Maps the editor's save-encoding names to a concrete <see cref="Encoding"/> and serializes
/// text to bytes for saving (prepending a BOM when the chosen encoding carries one). Names match
/// <see cref="TextEncodingDetector"/> output and the status-bar menu labels. UI-free and testable.
/// Registers the legacy code-pages provider so Windows-1251 is available under InvariantGlobalization.</summary>
public static class SaveEncoding
{
    public const string Utf8 = "UTF-8";
    public const string Utf8Bom = "UTF-8 BOM";
    public const string Utf16Le = "UTF-16 LE";
    public const string Utf16Be = "UTF-16 BE";
    public const string Windows1251 = "Windows-1251";

    static SaveEncoding() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>The save-encoding names offered in the UI, in menu order.</summary>
    public static readonly string[] Names = [Utf8, Utf8Bom, Utf16Le, Utf16Be, Windows1251];

    /// <summary>Resolve a name to an <see cref="Encoding"/>; an unknown name falls back to UTF-8 (no BOM).
    /// BOM-carrying encodings are configured to emit a preamble (see <see cref="GetBytes"/>).</summary>
    public static Encoding Resolve(string? encodingName) => encodingName switch
    {
        Utf8Bom => new UTF8Encoding(true),
        Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
        Windows1251 => Encoding.GetEncoding(1251),
        _ => new UTF8Encoding(false),
    };

    /// <summary>Serialize <paramref name="text"/> for saving under <paramref name="encodingName"/>.
    /// <see cref="Encoding.GetBytes(string)"/> never includes a BOM, so we prepend the encoding's
    /// preamble (empty for the no-BOM encodings, so this is a no-op there).</summary>
    public static byte[] GetBytes(string text, string? encodingName)
    {
        var encoding = Resolve(encodingName);
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        if (preamble.Length == 0)
            return body;

        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }
}
