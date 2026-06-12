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
        // S6: take the marker positions from the inserter (the indices where WE placed them) instead
        // of re-IndexOf-ing the result — a body that literally contains the marker tokens would
        // otherwise shift the offsets to the user's occurrence.
        var withMarkers = InsertFragmentMarkers(htmlDocument, out var fragStartInDoc, out var fragEndInDoc);
        var headerLength = string.Format(HeaderTemplate, 0, 0, 0, 0).Length; // ASCII → bytes == chars

        var bytes = Encoding.UTF8.GetBytes(withMarkers);
        var startHtml = headerLength;
        var endHtml = headerLength + bytes.Length;
        var startFragment = headerLength
            + Encoding.UTF8.GetByteCount(withMarkers[..fragStartInDoc]) + StartFragment.Length;
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
        => InsertFragmentMarkers(html, out _, out _);

    /// <summary>As above, also reporting the char index of each inserted marker in the result so the
    /// caller can compute byte offsets without re-searching (S6: robust if the body contains the
    /// literal marker tokens).</summary>
    internal static string InsertFragmentMarkers(string html, out int startFragmentIndex, out int endFragmentIndex)
    {
        var bodyOpen = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        var bodyClose = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyOpen >= 0 && bodyClose > bodyOpen
            && html.IndexOf('>', bodyOpen) is var contentStart && contentStart >= 0 && contentStart < bodyClose)
        {
            var prefix = html[..(contentStart + 1)];
            var middle = html[(contentStart + 1)..bodyClose];
            startFragmentIndex = prefix.Length;
            endFragmentIndex = prefix.Length + StartFragment.Length + middle.Length;
            return prefix + StartFragment + middle + EndFragment + html[bodyClose..];
        }

        startFragmentIndex = 0;
        endFragmentIndex = StartFragment.Length + html.Length;
        return StartFragment + html + EndFragment;
    }
}
