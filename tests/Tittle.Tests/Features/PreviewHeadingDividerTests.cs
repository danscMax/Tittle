using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

public class PreviewHeadingDividerTests
{
    // H1, H2, H3 — only H1/H2 get a rule.
    private const string Md = "# A\n\npara1\n\n## B\n\npara2\n\n### C\n\ntail";

    private static (Window Window, Panel Panel) Render()
    {
        var vm = DocumentTabViewModel.FromFile(Md, "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = view.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("Preview")!;
        PreviewHeadingDivider.AttachAll(preview);
        var heading = preview.GetVisualDescendants().OfType<Control>()
            .First(c => PreviewSectionCollapser.HeadingLevel(c) > 0);
        return (window, (Panel)heading.Parent!);
    }

    private static int DividerCount(Panel panel)
        => panel.Children.Count(c => c.Classes.Contains("heading-divider"));

    [AvaloniaFact]
    public void InsertsRule_AfterH1AndH2_NotH3()
    {
        var (window, panel) = Render();

        // Two rules (under H1 "A" and H2 "B"), each immediately after its heading; H3 "C" gets none.
        Assert.Equal(2, DividerCount(panel));
        foreach (var heading in panel.Children.OfType<Control>().ToList()
                     .Where(c => PreviewSectionCollapser.HeadingLevel(c) is 1 or 2))
        {
            var i = panel.Children.IndexOf(heading);
            Assert.Contains("heading-divider", panel.Children[i + 1].Classes);
        }

        var h3 = panel.Children.OfType<Control>()
            .First(c => PreviewSectionCollapser.HeadingLevel(c) == 3);
        var j = panel.Children.IndexOf(h3);
        Assert.False(j + 1 < panel.Children.Count
            && panel.Children[j + 1].Classes.Contains("heading-divider"));
        window.Close();
    }

    [AvaloniaFact]
    public void Idempotent_SecondCall_AddsNoDuplicateRules()
    {
        var (window, panel) = Render();
        var preview = panel.GetVisualAncestors().OfType<Markdown.Avalonia.MarkdownScrollViewer>().First();

        PreviewHeadingDivider.AttachAll(preview); // a later reflow tick must not re-insert
        PreviewHeadingDivider.AttachAll(preview);

        Assert.Equal(2, DividerCount(panel));
        window.Close();
    }
}
