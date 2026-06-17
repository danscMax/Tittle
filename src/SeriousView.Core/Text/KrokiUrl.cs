using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SeriousView.Core.Text;

/// <summary>Builds a Kroki <b>GET</b> URL for a diagram: <c>{base}/{type}/{format}/{payload}</c>,
/// where the payload is the diagram text zlib-deflated then base64url-encoded. Used by the HTML
/// export so a diagram becomes a plain <c>&lt;img&gt;</c> the browser fetches (the in-app preview
/// uses POST instead). Pure / BCL-only.</summary>
public static class KrokiUrl
{
    public static string Get(string baseUrl, string krokiType, string body)
        => $"{baseUrl.TrimEnd('/')}/{krokiType}/{DiagramTypes.FormatFor(krokiType)}/{Encode(body)}";

    /// <summary>zlib-deflate(UTF-8(body)) → base64url (no padding) — the Kroki GET encoding.</summary>
    public static string Encode(string body)
    {
        var raw = Encoding.UTF8.GetBytes(body);
        using var ms = new MemoryStream();
        using (var zlib = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw, 0, raw.Length);

        return Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
