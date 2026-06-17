using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tittle.Core.Text;

namespace Tittle.Core.Services;

/// <summary>A rendered diagram from Kroki: the image bytes plus whether they're SVG (vector) or a
/// raster format (PNG) — the UI turns SVG into an <c>SvgImage</c> and raster into a <c>Bitmap</c>.</summary>
public sealed record DiagramImage(byte[] Bytes, bool IsSvg);

/// <summary>Thin client for a Kroki diagram server (<c>POST {base}/{type}/{format}</c> with the raw
/// diagram text → image bytes). Pure transport, no Avalonia — the caller injects the
/// <see cref="HttpClient"/> (one shared instance) so it's unit-testable with a fake handler.
/// Diagram text is sent to the configured server, so the feature must stay opt-in.</summary>
public static class KrokiClient
{
    public static async Task<DiagramImage> RenderAsync(
        HttpClient http, string baseUrl, string krokiType, string body, CancellationToken ct = default)
    {
        var format = DiagramTypes.FormatFor(krokiType);
        var url = $"{baseUrl.TrimEnd('/')}/{krokiType}/{format}";

        using var content = new StringContent(body, Encoding.UTF8, "text/plain");
        using var response = await http.PostAsync(url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return new DiagramImage(bytes, IsSvg: string.Equals(format, "svg", StringComparison.Ordinal));
    }
}
