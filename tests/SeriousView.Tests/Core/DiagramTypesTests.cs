using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class DiagramTypesTests
{
    [Theory]
    [InlineData("mermaid", "mermaid")]
    [InlineData("plantuml", "plantuml")]
    [InlineData("puml", "plantuml")]
    [InlineData("dot", "graphviz")]
    [InlineData("graphviz", "graphviz")]
    [InlineData("c4", "c4plantuml")]
    [InlineData("vega-lite", "vegalite")]
    [InlineData("D2", "d2")] // case-insensitive
    public void ToKrokiType_MapsAliases(string fence, string expected)
    {
        Assert.True(DiagramTypes.IsDiagramLang(fence));
        Assert.Equal(expected, DiagramTypes.ToKrokiType(fence));
    }

    [Theory]
    [InlineData("python")]
    [InlineData("csharp")]
    [InlineData("")]
    [InlineData(null)]
    public void NonDiagramLanguages_AreNotDiagrams(string? fence)
    {
        Assert.False(DiagramTypes.IsDiagramLang(fence));
        Assert.Null(DiagramTypes.ToKrokiType(fence));
    }

    [Theory]
    [InlineData("mermaid", "png")] // foreignObject SVG → request PNG
    [InlineData("plantuml", "svg")]
    [InlineData("graphviz", "svg")]
    public void FormatFor_MermaidIsPng_RestSvg(string krokiType, string expected)
        => Assert.Equal(expected, DiagramTypes.FormatFor(krokiType));
}
