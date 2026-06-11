using System;
using System.Text;

namespace SeriousView.Core.Export;

/// <summary>
/// Builds the Windows CF_HTML clipboard payload ("HTML Format") for copy-as-rich-text
/// (ported): a fixed-width ASCII header whose offsets are BYTE positions into the UTF-8
/// payload, fragment markers slipped inside the document's &lt;body&gt;. Pure — the UI-side
/// clipboard service just hands the bytes to the OS.
/// </summary>
public static class ClipboardHtml
{
    private const string StartFragment = "<!--StartFragment-->";
    private const string EndFragment = "<!--EndFragment-->";

    // D10 keeps every offset the same width, so the header length is independent of values.
    private const string HeaderTemplate =
        "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

    /// <summary>Wraps a full HTML document into the CF_HTML payload (UTF-8 bytes).</summary>
    public static byte[] BuildCfHtml(string htmlDocument)
    {
        var withMarkers = InsertFragmentMarkers(htmlDocument);
        var headerLength = string.Format(HeaderTemplate, 0, 0, 0, 0).Length; // ASCII → bytes == chars

        var bytes = Encoding.UTF8.GetBytes(withMarkers);
        var startHtml = headerLength;
        var endHtml = headerLength + bytes.Length;
        var fragStartInDoc = withMarkers.IndexOf(StartFragment, StringComparison.Ordinal);
        var startFragment = headerLength
            + Encoding.UTF8.GetByteCount(withMarkers[..fragStartInDoc]) + StartFragment.Length;
        var fragEndInDoc = withMarkers.IndexOf(EndFragment, StringComparison.Ordinal);
        var endFragment = headerLength + Encoding.UTF8.GetByteCount(withMarkers[..fragEndInDoc]);

        var header = string.Format(HeaderTemplate, startHtml, endHtml, startFragment, endFragment);
        var payload = new byte[headerLength + bytes.Length];
        Encoding.ASCII.GetBytes(header, payload);
        bytes.CopyTo(payload, headerLength);
        return payload;
    }

    /// <summary>Fragment = the &lt;body&gt; content when present, the whole document otherwise.
    /// Internal for tests.</summary>
    internal static string InsertFragmentMarkers(string html)
    {
        var bodyOpen = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyOpen >= 0 && bodyClose > bodyOpen
            && html.IndexOf('>', bodyOpen) is var contentStart && contentStart >= 0 && contentStart < bodyClose)
        {
            return html[..(contentStart + 1)] + StartFragment
                + html[(contentStart + 1)..bodyClose] + EndFragment + html[bodyClose..];
        }

        return StartFragment + html + EndFragment;
    }
}
