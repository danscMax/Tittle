using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Tittle.Core.Services;
using Xunit;

namespace Tittle.Tests.Core;

public class KrokiClientTests
{
    private sealed class CapturingHandler(byte[] response) : HttpMessageHandler
    {
        public string? Url { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Url = request.RequestUri!.ToString();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(response) };
        }
    }

    [Fact]
    public async Task RenderAsync_Mermaid_PostsToPngEndpoint_NotSvg()
    {
        var handler = new CapturingHandler([1, 2, 3]);
        using var http = new HttpClient(handler);

        var image = await KrokiClient.RenderAsync(http, "https://kroki.io/", "mermaid", "graph TD;A-->B");

        Assert.Equal("https://kroki.io/mermaid/png", handler.Url); // trailing slash trimmed
        Assert.Equal("graph TD;A-->B", handler.Body);
        Assert.False(image.IsSvg);
        Assert.Equal([1, 2, 3], image.Bytes);
    }

    [Fact]
    public async Task RenderAsync_PlantUml_PostsToSvgEndpoint_AndMarksSvg()
    {
        var handler = new CapturingHandler([4, 5]);
        using var http = new HttpClient(handler);

        var image = await KrokiClient.RenderAsync(http, "https://kroki.io", "plantuml", "@startuml\nA->B\n@enduml");

        Assert.Equal("https://kroki.io/plantuml/svg", handler.Url);
        Assert.True(image.IsSvg);
    }

    [Fact]
    public async Task RenderAsync_HttpError_Throws()
    {
        var handler = new ErrorHandler();
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => KrokiClient.RenderAsync(http, "https://kroki.io", "graphviz", "digraph{}"));
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
    }
}
