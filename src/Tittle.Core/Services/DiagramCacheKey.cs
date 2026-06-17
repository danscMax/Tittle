using System;
using System.Security.Cryptography;
using System.Text;
using Tittle.Core.Text;

namespace Tittle.Core.Services;

/// <summary>Deterministic cache filename for a rendered diagram, keyed by the server URL + Kroki
/// type + body (a content hash). The extension follows the render format (PNG for Mermaid, SVG
/// otherwise). Pure — the UI layer joins it with the on-disk cache directory.</summary>
public static class DiagramCacheKey
{
    public static string FileName(string url, string krokiType, string body)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{url}\n{krokiType}\n{body}")));
        return $"{hash}.{DiagramTypes.FormatFor(krokiType)}";
    }
}
