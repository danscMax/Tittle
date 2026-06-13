using System;
using System.Collections.Generic;

namespace SeriousView.Core.Text;

/// <summary>Maps a fenced-code-block language (```mermaid, ```plantuml, ```dot, …) to its Kroki
/// diagram type and output format. Pure — the markdown preprocessor uses it to recognise diagram
/// fences and the Kroki client uses it to build the request URL. Mermaid renders as PNG (its SVG
/// uses <c>&lt;foreignObject&gt;</c>, which the vector renderer can't draw); everything else as SVG.</summary>
public static class DiagramTypes
{
    // Fence language (lower-case) → canonical Kroki type. Aliases collapse to the Kroki name.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mermaid"] = "mermaid",
        ["plantuml"] = "plantuml",
        ["puml"] = "plantuml",
        ["uml"] = "plantuml",
        ["c4"] = "c4plantuml",
        ["c4plantuml"] = "c4plantuml",
        ["dot"] = "graphviz",
        ["gv"] = "graphviz",
        ["graphviz"] = "graphviz",
        ["d2"] = "d2",
        ["dbml"] = "dbml",
        ["erd"] = "erd",
        ["ditaa"] = "ditaa",
        ["nomnoml"] = "nomnoml",
        ["blockdiag"] = "blockdiag",
        ["seqdiag"] = "seqdiag",
        ["actdiag"] = "actdiag",
        ["nwdiag"] = "nwdiag",
        ["packetdiag"] = "packetdiag",
        ["rackdiag"] = "rackdiag",
        ["bpmn"] = "bpmn",
        ["bytefield"] = "bytefield",
        ["excalidraw"] = "excalidraw",
        ["pikchr"] = "pikchr",
        ["structurizr"] = "structurizr",
        ["svgbob"] = "svgbob",
        ["symbolator"] = "symbolator",
        ["tikz"] = "tikz",
        ["umlet"] = "umlet",
        ["vega"] = "vega",
        ["vegalite"] = "vegalite",
        ["vega-lite"] = "vegalite",
        ["wavedrom"] = "wavedrom",
        ["wireviz"] = "wireviz",
    };

    /// <summary>True when <paramref name="fenceLang"/> names a diagram Kroki can render.</summary>
    public static bool IsDiagramLang(string? fenceLang)
        => fenceLang is not null && Aliases.ContainsKey(fenceLang.Trim());

    /// <summary>Canonical Kroki type for a fence language, or null if it isn't a diagram.</summary>
    public static string? ToKrokiType(string? fenceLang)
        => fenceLang is not null && Aliases.TryGetValue(fenceLang.Trim(), out var t) ? t : null;

    /// <summary>Output format for a Kroki type: PNG for Mermaid (foreignObject SVG), SVG otherwise.</summary>
    public static string FormatFor(string krokiType)
        => string.Equals(krokiType, "mermaid", StringComparison.OrdinalIgnoreCase) ? "png" : "svg";
}
