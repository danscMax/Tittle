using System;
using Avalonia.Controls;
using Avalonia.Media;
using Markdown.Avalonia.Utils;
using MdEngine = Markdown.Avalonia.Markdown;

namespace SeriousView.Features.Viewer;

/// <summary>
/// Renders <c>::: admonition-&lt;type&gt;</c> container blocks (produced by the Core
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

    // From the preprocessor the name is "admonition-<type>"; accept a bare "<type>" too.
    private static string NormalizeType(string blockName)
    {
        var name = blockName.StartsWith("admonition-", StringComparison.OrdinalIgnoreCase)
            ? blockName["admonition-".Length..]
            : blockName;
        name = name.Trim().ToLowerInvariant();

        return name switch
        {
            "note" or "tip" or "important" or "warning" or "caution" => name,
            _ => "note", // unknown container → neutral note styling
        };
    }

    private static string TitleFor(string type) => type switch
    {
        "tip" => "Совет",
        "important" => "Важно",
        "warning" => "Предупреждение",
        "caution" => "Осторожно",
        _ => "Примечание",
    };
}
