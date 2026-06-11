using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SeriousView.Features.Viewer;

/// <summary>Collapsible heading sections in the preview (ported `.collapse-icon`):
/// clicking a heading hides everything up to the next heading of the same or shallower
/// level. The hidden controls STAY in the visual tree (IsVisible only), so the M10
/// heading-tops cache keeps its ordinal↔control contract; the resulting extent change
/// invalidates the cached Ys automatically.</summary>
public static class PreviewSectionCollapser
{
    private const string AttachedClass = "collapsible-attached";
    private const string CollapsedClass = "section-collapsed";

    /// <summary>Wires every not-yet-wired top-level heading (idempotent; called from the
    /// preview's reflow pass alongside the table sorter and code-block fixups).</summary>
    public static void AttachAll(Visual previewRoot)
    {
        foreach (var panel in TopLevelPanels(previewRoot))
        {
            foreach (var child in panel.Children.OfType<Control>().ToList())
            {
                if (HeadingLevel(child) == 0 || child.Classes.Contains(AttachedClass))
                    continue;

                child.Classes.Add(AttachedClass);
                child.Cursor = new Cursor(StandardCursorType.Hand);
                ToolTip.SetTip(child, "Клик — свернуть/развернуть секцию");
                var heading = child;
                child.PointerPressed += (_, e) =>
                {
                    Toggle(panel, heading);
                    e.Handled = true;
                };
            }
        }
    }

    /// <summary>Hides/shows the body below <paramref name="heading"/>. Expanding restores
    /// every child (nested collapsed sections reopen — same as the original). Internal so
    /// headless tests can drive it without synthesizing pointer events.</summary>
    internal static void Toggle(Panel panel, Control heading)
    {
        var index = panel.Children.IndexOf(heading);
        if (index < 0)
            return;

        var level = HeadingLevel(heading);
        var section = new List<Control>();
        for (var i = index + 1; i < panel.Children.Count; i++)
        {
            var sibling = panel.Children[i];
            var siblingLevel = HeadingLevel(sibling);
            if (siblingLevel != 0 && siblingLevel <= level)
                break;
            section.Add(sibling);
        }

        if (section.Count == 0)
            return;

        var collapse = section.Any(c => c.IsVisible);
        foreach (var control in section)
            control.IsVisible = !collapse;

        if (collapse)
            heading.Classes.Add(CollapsedClass);
        else
            heading.Classes.Remove(CollapsedClass);
    }

    // Only the document's own panel — admonition bodies render through the same engine and
    // carry the same class, but their headings are out of the outline contract.
    private static IEnumerable<StackPanel> TopLevelPanels(Visual root)
        => root.GetVisualDescendants().OfType<StackPanel>()
            .Where(p => p.Classes.Contains("Markdown_Avalonia_MarkdownViewer")
                        && !p.GetVisualAncestors().OfType<StackPanel>()
                            .Any(a => a.Classes.Contains("Markdown_Avalonia_MarkdownViewer")));

    internal static int HeadingLevel(StyledElement control)
    {
        foreach (var cls in control.Classes)
        {
            if (cls.Length == 8 && cls.StartsWith("Heading", StringComparison.Ordinal)
                && cls[7] is >= '1' and <= '6')
                return cls[7] - '0';
        }

        return 0;
    }
}
