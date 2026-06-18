using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Tittle.Features.Viewer;

/// <summary>A thin rule line under top-level H1/H2 headings in the preview — the GitHub /
/// VS-Code reading look. <c>CTextBlock</c> exposes no border of its own, so we insert a 1px
/// <see cref="Border"/> sibling right after each H1/H2 in the document panel. Idempotent: a
/// divided heading carries a marker class, so the reflow pass can call this every tick. The
/// inserted Border is a plain body sibling (<see cref="PreviewSectionCollapser.HeadingLevel"/>
/// 0), so the section collapser folds it away with its heading and the M10 heading-tops
/// contract (which only tracks heading controls) is untouched.</summary>
public static class PreviewHeadingDivider
{
    private const string AttachedClass = "heading-divider-attached";
    private const string DividerClass = "heading-divider";

    /// <summary>Inserts the rule after every not-yet-divided H1/H2 (idempotent; called from the
    /// preview reflow pass alongside the table sorter and section collapser).</summary>
    public static void AttachAll(Visual previewRoot)
    {
        foreach (var panel in PreviewSectionCollapser.TopLevelPanels(previewRoot))
        {
            // Snapshot: we mutate panel.Children while deciding what to insert.
            foreach (var child in panel.Children.OfType<Control>().ToList())
            {
                var level = PreviewSectionCollapser.HeadingLevel(child);
                if (level is not (1 or 2) || child.Classes.Contains(AttachedClass))
                    continue;

                child.Classes.Add(AttachedClass);
                var index = panel.Children.IndexOf(child);
                if (index < 0)
                    continue;

                var divider = new Border
                {
                    Height = 1,
                    // H1's rule sits a touch lower (its larger glyph leaves more visual gap).
                    Margin = new Thickness(0, level == 1 ? 3 : 1, 0, 11),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                divider.Classes.Add(DividerClass);
                // Live theme-following, same token the table grid lines use (DRY).
                divider.Bind(Border.BackgroundProperty, divider.GetResourceObservable("TableBorderBrush"));
                panel.Children.Insert(index + 1, divider);
            }
        }
    }
}
