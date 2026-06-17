using System;
using System.IO;
using System.IO.Compression;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class KrokiUrlTests
{
    [Fact]
    public void Get_BuildsTypeFormatPayloadUrl_MermaidIsPng()
    {
        var url = KrokiUrl.Get("https://kroki.io/", "mermaid", "graph TD;A-->B");

        Assert.StartsWith("https://kroki.io/mermaid/png/", url); // trailing slash trimmed, mermaid → png
        var payload = url["https://kroki.io/mermaid/png/".Length..];
        Assert.DoesNotContain('+', payload); // base64url
        Assert.DoesNotContain('/', payload);
        Assert.DoesNotContain('=', payload);
        Assert.NotEqual(0, payload.Length);
    }

    [Fact]
    public void Encode_IsZlibDeflateBase64Url_RoundTrips()
    {
        const string body = "graph TD;A-->B";
        var encoded = KrokiUrl.Encode(body);

        var b64 = encoded.Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight((b64.Length + 3) / 4 * 4, '=');
        using var ms = new MemoryStream(Convert.FromBase64String(b64));
        using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
        using var reader = new StreamReader(zlib);
        Assert.Equal(body, reader.ReadToEnd());
    }
}
