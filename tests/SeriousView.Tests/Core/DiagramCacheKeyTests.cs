using SeriousView.Core.Services;
using Xunit;

namespace SeriousView.Tests.Core;

public class DiagramCacheKeyTests
{
    [Fact]
    public void FileName_IsDeterministic_AndUsesTheFormatExtension()
    {
        var a = DiagramCacheKey.FileName("https://kroki.io", "mermaid", "body");
        Assert.Equal(a, DiagramCacheKey.FileName("https://kroki.io", "mermaid", "body")); // deterministic
        Assert.EndsWith(".png", a);                                                       // mermaid → png
        Assert.EndsWith(".svg", DiagramCacheKey.FileName("https://kroki.io", "plantuml", "body"));
    }

    [Theory]
    [InlineData("https://kroki.io", "mermaid", "b2")] // different body
    [InlineData("https://other", "mermaid", "body")]  // different url
    [InlineData("https://kroki.io", "graphviz", "body")] // different type (also different ext)
    public void FileName_DiffersWhenAnyInputChanges(string url, string type, string body)
        => Assert.NotEqual(
            DiagramCacheKey.FileName("https://kroki.io", "mermaid", "body"),
            DiagramCacheKey.FileName(url, type, body));
}
