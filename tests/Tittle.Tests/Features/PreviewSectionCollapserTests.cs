using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

public class PreviewSectionCollapserTests
{
    private const string Md = "# A\n\npara1\n\n## B\n\npara2\n\n# C\n\ntail";

    private static (Window Window, Panel Panel, Control[] Headings) Render()
    {
        var vm = DocumentTabViewModel.FromFile(Md, "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = view.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("Preview")!;
        PreviewSectionCollapser.AttachAll(preview);
        var headings = preview.GetVisualDescendants().OfType<Control>()
            .Where(c => PreviewSectionCollapser.HeadingLevel(c) > 0)
            .ToArray();
        var panel = (Panel)headings[0].Parent!;
        return (window, panel, headings);
    }

    [AvaloniaFact]
    public void Toggle_TopHeading_HidesUntilTheNextSameLevel()
    {
        var (window, panel, headings) = Render();
        Assert.Equal(3, headings.Length); // A, B, C
        Assert.Contains("collapsible-attached", headings[0].Classes);

        PreviewSectionCollapser.Toggle(panel, headings[0]);

        var indexA = panel.Children.IndexOf(headings[0]);
        var indexC = panel.Children.IndexOf(headings[2]);
        foreach (var child in panel.Children.Skip(indexA + 1).Take(indexC - indexA - 1))
            Assert.False(child.IsVisible);
        Assert.True(headings[2].IsVisible);       // the next # stays
        Assert.True(panel.Children[^1].IsVisible); // its body stays
        Assert.Contains("section-collapsed", headings[0].Classes);

        PreviewSectionCollapser.Toggle(panel, headings[0]); // expand restores everything
        Assert.All(panel.Children, c => Assert.True(c.IsVisible));
        Assert.DoesNotContain("section-collapsed", headings[0].Classes);
        window.Close();
    }

    [AvaloniaFact]
    public void Toggle_NestedHeading_OnlyItsOwnBody()
    {
        var (window, panel, headings) = Render();

        PreviewSectionCollapser.Toggle(panel, headings[1]); // ## B

        Assert.True(headings[0].IsVisible);
        Assert.True(headings[1].IsVisible);
        Assert.True(headings[2].IsVisible);
        // B's own section: its paragraph plus the H2 rule the reflow pass inserts right after B
        // (PreviewHeadingDivider) — both live below B and fold away with it. No OTHER section moves.
        Assert.Equal(2, panel.Children.Count(c => !c.IsVisible));
        Assert.False(panel.Children.OfType<Control>()
            .First(c => c.Classes.Contains("heading-divider")
                        && panel.Children.IndexOf(c) > panel.Children.IndexOf(headings[1])
                        && panel.Children.IndexOf(c) < panel.Children.IndexOf(headings[2]))
            .IsVisible);
        window.Close();
    }
}
