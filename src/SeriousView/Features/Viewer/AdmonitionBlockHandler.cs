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
}
