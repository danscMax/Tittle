using System.Net;
using Markdig;
using SeriousView.Core.Text;

namespace SeriousView.Core.Export;

/// <summary>
/// Self-contained HTML export (M13): RAW markdown → one portable file with an inline themed
/// stylesheet — no external resources, opens anywhere. Goes through Markdig (CommonMark + GFM
/// advanced extensions: tables, task lists, footnotes, autolinks), NOT through the viewer's
/// preprocessor — its glyph/container rewrites are crutches for the in-app renderer that
/// Markdig doesn't need. Wiki links are the one shared dialect: <c>[[name]]</c> becomes a
/// relative <c>name.md</c> link when the sibling resolves (same token regex as the viewer
/// pass), else plain text. Block math stays as authored — a self-contained file without JS
/// cannot render LaTeX (documented limitation).
/// </summary>
public static class HtmlExporter
{
    // YAML front-matter is consumed, not rendered: the viewer shows it as a metadata panel,
    // but in a portable HTML file raw YAML would just be noise.
    // DisableHtml is a SECURITY boundary, not a styling choice: the export is written to disk,
    // copied to the clipboard as CF_HTML and shell-opened in a BROWSER (print path) — raw
    // passthrough would let a hostile document execute <script> outside the app. Markdig
    // escapes raw HTML to visible text instead, which matches a viewer's intent.
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().DisableHtml().UseAdvancedExtensions().UseYamlFrontMatter().Build();

    public static string Export(
        string markdown, string title, bool darkTheme, Func<string, bool>? wikiLinkResolver = null)
    {
        var prepared = ConvertWikiLinks(markdown ?? string.Empty, wikiLinkResolver);
        var body = Markdown.ToHtml(prepared, Pipeline);
        var safeTitle = WebUtility.HtmlEncode(title);

        return $"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{safeTitle}</title>
            <style>{(darkTheme ? DarkCss : LightCss)}{SharedCss}</style>
            </head>
            <body>
            <main>
            {body}
            </main>
            </body>
            </html>
            """;
    }

    /// <summary>[[name]] → a relative markdown link to the sibling note (Markdig renders the
    /// anchor), or plain text when nothing resolves. Fence/inline-code aware, like the viewer.</summary>
    private static string ConvertWikiLinks(string markdown, Func<string, bool>? resolve)
    {
        if (!markdown.Contains("[[", StringComparison.Ordinal))
            return markdown;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var regions = MarkdownCodeRegions.Scan(lines);
        for (var i = 0; i < lines.Length; i++)
        {
            if (regions.IsFencedLine(i) || !lines[i].Contains("[[", StringComparison.Ordinal))
                continue;

            lines[i] = MarkdownCodeRegions.ReplaceOutsideCode(
                lines[i], MarkdownPreprocessor.WikiTokenRegex, m =>
                {
                    var name = m.Groups[1].Value.Trim();
                    if (name.Length == 0)
                        return m.Value;
                    if (!WikiLink.IsValidName(name) || resolve?.Invoke(name) != true)
                        return name;
                    return $"[{name}]({Uri.EscapeDataString(name)}.md)";
                });
        }

        return string.Join("\n", lines);
    }

    // Palettes loosely mirror the app themes; layout/typography shared below.
    private const string DarkCss =
        ":root{--bg:#14141b;--fg:#e7e7ef;--muted:#9b9ba8;--accent:#6ea8e0;--surface:#1d1d27;--border:#32323f;}";

    private const string LightCss =
        ":root{--bg:#ffffff;--fg:#1b1b24;--muted:#6a6a76;--accent:#2a6db0;--surface:#f5f5f8;--border:#dcdce3;}";

    private const string SharedCss = """

        html{background:var(--bg);color:var(--fg);}
        body{margin:0;font:16px/1.65 'Segoe UI',system-ui,sans-serif;}
        main{max-width:860px;margin:0 auto;padding:40px 32px 64px;}
        h1,h2,h3,h4,h5,h6{line-height:1.3;margin:1.4em 0 .5em;}
        h1{font-size:2em;border-bottom:1px solid var(--border);padding-bottom:.3em;}
        h2{font-size:1.5em;border-bottom:1px solid var(--border);padding-bottom:.25em;}
        a{color:var(--accent);text-decoration:none;}
        a:hover{text-decoration:underline;}
        code{background:var(--surface);border:1px solid var(--border);border-radius:4px;
             padding:.1em .35em;font:.9em 'Cascadia Code',Consolas,monospace;}
        pre{background:var(--surface);border:1px solid var(--border);border-radius:8px;
            padding:12px 14px;overflow-x:auto;}
        pre code{background:none;border:none;padding:0;}
        blockquote{margin:1em 0;padding:.2em 1em;border-left:3px solid var(--accent);
                   color:var(--muted);}
        table{border-collapse:collapse;margin:1em 0;display:block;overflow-x:auto;}
        th,td{border:1px solid var(--border);padding:6px 12px;}
        th{background:var(--surface);}
        img{max-width:100%;}
        hr{border:none;border-top:1px solid var(--border);margin:2em 0;}
        input[type=checkbox]{margin-right:.45em;}
        sup a{font-size:.8em;}
        """;
}
