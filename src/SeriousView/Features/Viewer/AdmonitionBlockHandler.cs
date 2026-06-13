using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Markdown.Avalonia.Utils;
using SeriousView.Core.Services;
using SeriousView.Core.Text;
using MdEngine = Markdown.Avalonia.Markdown;

namespace SeriousView.Features.Viewer;

/// <summary>
/// Renders <c>::: &lt;type&gt;</c> container blocks (produced by the Core
/// <c>MarkdownPreprocessor</c> from GitHub <c>&gt; [!NOTE]</c> alerts) as themed callout
/// boxes. Colours come entirely from <c>Themes/Admonitions.axaml</c> via style classes
/// (<c>Border.admonition</c> + <c>Border.admonition-&lt;type&gt;</c>), so the boxes follow
/// Light/Dark. The body is rendered as markdown by the same engine, so inline formatting,
/// links and nested lists work inside a callout.
/// </summary>
public sealed class AdmonitionBlockHandler : IContainerBlockHandler
{
    private readonly MdEngine _engine;
    private readonly Func<string?>? _krokiUrlProvider;

    // One shared client + an in-memory cache (keyed by url\ntype\nbody) so identical diagrams
    // aren't re-fetched on every preview rebuild. Static: shared across tabs/handlers.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly ConcurrentDictionary<string, DiagramImage> DiagramCache = new();

    public AdmonitionBlockHandler(MdEngine engine, Func<string?>? krokiUrlProvider = null)
    {
        _engine = engine;
        _krokiUrlProvider = krokiUrlProvider;
    }

    public Border ProvideControl(string assetPathRoot, string blockName, string lines)
    {
        // ::: math (from the preprocessor's $$…$$ / \[…\] pass, M11): a native LaTeX block.
        // The body arrives percent-encoded (an opaque transport that survives whatever the
        // container parser does to raw bodies) — decode it first. Parse errors render inline
        // as the library's error text — never a crash on garbage.
        if (string.Equals(blockName.Trim(), "math", StringComparison.OrdinalIgnoreCase))
        {
            var math = new CSharpMath.Avalonia.MathView
            {
                LaTeX = System.Uri.UnescapeDataString(lines.Trim()),
                DisplayErrorInline = true,
                FontSize = 18,
            };
            var mathBorder = new Border { Child = math };
            mathBorder.Classes.Add("math-block");
            return mathBorder;
        }

        // ::: frontmatter (ported): the leading YAML block rendered as a metadata panel.
        // Same percent-encoded transport as math; top-level "key: value" lines become rows,
        // anything else (nested YAML, lists) shows as a raw continuation line.
        if (string.Equals(blockName.Trim(), "frontmatter", StringComparison.OrdinalIgnoreCase))
            return BuildFrontMatterPanel(System.Uri.UnescapeDataString(lines.Trim()));

        // ::: diagram (M12, opt-in): "type|body", both percent-encoded. Rendered via Kroki — a
        // placeholder shows immediately, the image lands when the async request returns.
        if (string.Equals(blockName.Trim(), "diagram", StringComparison.OrdinalIgnoreCase))
            return BuildDiagram(lines.Trim());

        var type = NormalizeType(blockName);

        var title = new TextBlock { Text = TitleFor(type) };
        title.Classes.Add("admonition-title");

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(title);
        body.Children.Add(_engine.Transform(lines));

        var border = new Border { Child = body };
        border.Classes.Add("admonition");
        border.Classes.Add("admonition-" + type);
        return border;
    }

    // The preprocessor emits the bare alert type as the container name ("note", "tip", …).
    // Unknown containers fall back to neutral "note" styling.
    private static string NormalizeType(string blockName)
    {
        var name = blockName.Trim().ToLowerInvariant();
        return name is "tip" or "important" or "warning" or "caution" ? name : "note";
    }

    private static string TitleFor(string type) => type switch
    {
        "tip" => "Совет",
        "important" => "Важно",
        "warning" => "Предупреждение",
        "caution" => "Осторожно",
        _ => "Примечание",
    };

    private static Border BuildFrontMatterPanel(string yaml)
    {
        var title = new TextBlock { Text = "Метаданные" };
        title.Classes.Add("admonition-title");

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
        };
        var row = 0;
        foreach (var line in yaml.Split('\n'))
        {
            if (line.Trim().Length == 0)
                continue;

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var colon = line.IndexOf(':');
            var isPair = colon > 0 && line.Length > 0 && !char.IsWhiteSpace(line[0]);

            var key = new TextBlock
            {
                Text = isPair ? line[..colon].Trim() : string.Empty,
                Margin = new Avalonia.Thickness(0, 1, 14, 1),
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
            };
            key.Classes.Add("frontmatter-key");
            Grid.SetRow(key, row);

            var value = new TextBlock
            {
                Text = isPair ? line[(colon + 1)..].Trim() : line.TrimEnd(),
                Margin = new Avalonia.Thickness(0, 1, 0, 1),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            value.Classes.Add("frontmatter-value");
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);

            grid.Children.Add(key);
            grid.Children.Add(value);
            row++;
        }

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(title);
        body.Children.Add(grid);

        var border = new Border { Child = body };
        border.Classes.Add("admonition");
        border.Classes.Add("admonition-note");
        border.Classes.Add("frontmatter-block");
        return border;
    }

    // "type|body" — both percent-encoded (the opaque ::: transport). Returns a Border that first
    // shows a placeholder, then swaps in the rendered diagram (or a source-code fallback on error).
    private Border BuildDiagram(string encoded)
    {
        var border = new Border();
        border.Classes.Add("diagram-block");

        var sep = encoded.IndexOf('|');
        if (sep < 0)
        {
            border.Child = DiagramError("Некорректный блок диаграммы", "");
            return border;
        }

        var type = Uri.UnescapeDataString(encoded[..sep]);
        var body = Uri.UnescapeDataString(encoded[(sep + 1)..]);
        var url = _krokiUrlProvider?.Invoke();

        if (string.IsNullOrWhiteSpace(url))
        {
            border.Child = DiagramError("Не задан адрес сервера Kroki", body);
            return border;
        }

        border.Child = new TextBlock
        {
            Text = $"Рендеринг диаграммы ({type})…",
            Foreground = ThemedMuted(),
            Margin = new Avalonia.Thickness(4),
        };

        _ = RenderDiagramAsync(border, url!, type, body);
        return border;
    }

    private static async Task RenderDiagramAsync(Border border, string url, string type, string body)
    {
        var key = $"{url}\n{type}\n{body}";
        try
        {
            if (!DiagramCache.TryGetValue(key, out var image))
            {
                image = await KrokiClient.RenderAsync(Http, url, type, body, CancellationToken.None);
                DiagramCache[key] = image;
            }

            var control = BuildImageControl(image);
            await Dispatcher.UIThread.InvokeAsync(() => border.Child = control);
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                border.Child = DiagramError($"Не удалось отрендерить диаграмму: {ex.Message}", body));
        }
    }

    private static Control BuildImageControl(DiagramImage image)
    {
        IImage source = image.IsSvg
            ? new SvgImage { Source = SvgSource.LoadFromSvg(System.Text.Encoding.UTF8.GetString(image.Bytes)) }
            : new Bitmap(new MemoryStream(image.Bytes));

        // Natural size, but never upscale past the diagram's own width; the preview's horizontal
        // scroll handles anything wider than the reading column.
        return new Image
        {
            Source = source,
            Stretch = Stretch.Uniform,
            MaxWidth = source.Size.Width > 0 ? source.Size.Width : double.PositiveInfinity,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        };
    }

    // On any failure, show the diagram source so its content is never lost, plus the reason.
    private static StackPanel DiagramError(string message, string source)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ThemedMuted(),
        });
        if (source.Length > 0)
            panel.Children.Add(new TextBlock
            {
                Text = source,
                FontFamily = new FontFamily("Cascadia Code,Consolas,monospace"),
                TextWrapping = TextWrapping.Wrap,
            });
        return panel;
    }

    private static IBrush ThemedMuted()
        => Application.Current is { } app
            && app.TryFindResource("ChromeForegroundMutedBrush", out var v) && v is IBrush b
            ? b : Brushes.Gray;
}
