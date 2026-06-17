using System;
using System.Collections.Generic;

namespace Tittle.Core.Text;

/// <summary>
/// The single <c>[[name]]</c> → markdown-link pass, shared by the in-app preprocessor
/// (<see cref="MarkdownPreprocessor"/>, which targets <c>wiki:</c> URLs) and the HTML exporter
/// (<see cref="Export.HtmlExporter"/>, which targets relative <c>.md</c> links). Only the URL
/// format differs — the scan, the guard set (fenced / overlong / link-reference-definition
/// lines), the name validation and the resolver contract are identical, so they live here once
/// and the two passes can never drift apart in what they skip.
/// </summary>
public static class WikiLinkRewriter
{
    /// <summary>Rewrites every <c>[[name]]</c> outside code on each eligible line in place.
    /// <paramref name="formatUrl"/> turns a validated, resolved name into the link destination
    /// (e.g. <c>wiki:…</c> for the viewer, <c>name.md</c> for export). Names that are empty,
    /// invalid or unresolved degrade to plain text; the token itself stays when the inner name
    /// is blank.</summary>
    public static void Rewrite(
        IList<string> lines, MarkdownCodeRegions regions,
        Func<string, bool>? resolve, Func<string, string> formatUrl)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (regions.IsFencedLine(i) || line.Length > MarkdownPreprocessor.MaxInlineLine
                || !line.Contains("[[", StringComparison.Ordinal)
                || MarkdownPreprocessor.IsLinkRefDefLine(line))
                continue;

            lines[i] = MarkdownCodeRegions.ReplaceOutsideCode(line, MarkdownPreprocessor.WikiTokenRegex, m =>
            {
                var name = m.Groups[1].Value.Trim();
                if (name.Length == 0)
                    return m.Value;
                if (!WikiLink.IsValidName(name) || resolve?.Invoke(name) != true)
                    return name;
                return $"[{name}]({formatUrl(name)})";
            });
        }
    }
}
