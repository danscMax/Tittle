using Avalonia.Controls;
using Markdown.Avalonia.Utils;
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

    public AdmonitionBlockHandler(MdEngine engine) => _engine = engine;

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
}
