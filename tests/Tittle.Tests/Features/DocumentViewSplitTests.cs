using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tittle.Core.Settings;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Tittle.Shared;
using Xunit;

namespace Tittle.Tests.Features;

public class DocumentViewSplitTests
{
    private static (Window window, DocumentView view, DocumentTabViewModel vm) Open(
        string markdown = "# A\n\ntext\n\n## B\n\nmore", SplitOrientation orientation = SplitOrientation.Horizontal)
    {
        var vm = DocumentTabViewModel.FromFile(markdown, "/docs/readme.md");
        vm.Layout = new LayoutOptions { SplitOrientation = orientation };
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 900, Height = 500, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, vm);
    }

    [AvaloniaFact]
    public void Split_ShowsBothPanes_AndSplitter()
    {
        var (window, view, vm) = Open();

        vm.ToggleSplitCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowSplit);
        Assert.True(view.SplitSplitterVisibleForTest);
        var (src, prev) = view.SplitTracksForTest;
        Assert.True(src > 0 && prev > 0); // both panes share the space
        // Both subtrees stay realized in the tree (no reparent/duplicate).
        Assert.NotNull(view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().FirstOrDefault());
        Assert.NotNull(view.GetVisualDescendants().OfType<Markdown.Avalonia.MarkdownScrollViewer>().FirstOrDefault());

        window.Close();
    }

    [AvaloniaFact]
    public void SingleMode_CollapsesTheOtherTrack_AndHidesSplitter()
    {
        var (window, view, vm) = Open();

        // Preview (default): source track collapsed, splitter hidden.
        Assert.False(view.SplitSplitterVisibleForTest);
        var preview = view.SplitTracksForTest;
        Assert.Equal(0, preview.Source);
        Assert.True(preview.Preview > 0);

        vm.ShowSourceModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var source = view.SplitTracksForTest;
        Assert.True(source.Source > 0);
        Assert.Equal(0, source.Preview);
        Assert.False(view.SplitSplitterVisibleForTest);

        window.Close();
    }

    [AvaloniaFact]
    public void Split_AppliesPersistedRatio()
    {
        var (window, view, vm) = Open();
        vm.Layout!.SplitRatio = 0.7;

        vm.ToggleSplitCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var (src, prev) = view.SplitTracksForTest;
        Assert.Equal(0.7, src / (src + prev), precision: 3);

        window.Close();
    }

    [AvaloniaFact]
    public void Split_OrientationFlip_SwapsGridAxis_AndKeepsPanesRealized()
    {
        var (window, view, vm) = Open();
        vm.ToggleSplitCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.SplitIsHorizontalForTest);

        vm.Layout!.SplitOrientation = SplitOrientation.Vertical;
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.SplitIsHorizontalForTest); // now Rows-based
        Assert.True(view.SplitSplitterVisibleForTest);
        var (src, prev) = view.SplitTracksForTest;
        Assert.True(src > 0 && prev > 0);
        // The single Source/Preview subtrees survive the flip (not torn down).
        Assert.NotNull(view.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().FirstOrDefault());
        Assert.NotNull(view.GetVisualDescendants().OfType<Markdown.Avalonia.MarkdownScrollViewer>().FirstOrDefault());

        window.Close();
    }

    [AvaloniaFact]
    public void Split_DragCapturesRatio_IntoSharedLayout()
    {
        var (window, view, vm) = Open();
        vm.ToggleSplitCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        view.SimulateSplitDragForTest(0.65); // the splitter rewrites the star tracks
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.65, vm.Layout!.SplitRatio, precision: 3); // read back into the shared layout

        window.Close();
    }

    // A document long enough to scroll in both panes inside the test viewport.
    private static string LongMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < 40; i++)
            sb.Append($"# Heading {i}\n\nParagraph {i} with enough words to take vertical room.\n\n");
        return sb.ToString();
    }

    private static (Window window, DocumentView view, DocumentTabViewModel vm) OpenScrollableSplit()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.Layout = new LayoutOptions();
        vm.IsActive = true; // sync runs only on the active tab
        var view = new DocumentView { DataContext = vm };
        // Hide BOTH scrollbars on both panes: a visible FluentAvalonia scrollbar shapes its arrow
        // glyphs via the "Symbols" font, which can't load headless (project memory).
        view.PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.Source.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.Source.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        var window = new Window { Width = 900, Height = 480, Content = view };
        window.Show();
        vm.ToggleSplitCommand.Execute(null); // enter split
        Dispatcher.UIThread.RunJobs();
        return (window, view, vm);
    }

    [AvaloniaFact]
    public void Split_PreviewScroll_DrivesSource_AndConverges()
    {
        var (window, view, vm) = OpenScrollableSplit();
        Assert.True(vm.ShowSplit);

        view.PreviewScroll.Offset = view.PreviewScroll.Offset.WithY(400);
        Dispatcher.UIThread.RunJobs();

        // The source pane followed the preview, and the feedback loop converged (RunJobs would hang
        // on a runaway loop; the echo suppression keeps the apply count bounded).
        Assert.True(view.Source.TextArea.TextView.ScrollOffset.Y > 0, "source should have followed the preview");
        Assert.InRange(view.SplitSyncApplyCount, 1, 8);

        window.Close();
    }

    [AvaloniaFact]
    public void Split_SourceScroll_DrivesPreview_AndConverges()
    {
        var (window, view, vm) = OpenScrollableSplit();
        var srcScroll = view.Source.GetVisualDescendants().OfType<ScrollViewer>().First();

        srcScroll.Offset = srcScroll.Offset.WithY(350);
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.PreviewScroll.Offset.Y > 0, "preview should have followed the source");
        Assert.InRange(view.SplitSyncApplyCount, 1, 8);

        window.Close();
    }

    [AvaloniaFact]
    public void Split_SourceIsMaster_DrivesActiveHeading()
    {
        var (window, view, vm) = OpenScrollableSplit();

        // Scrolling in split keeps the source pane as master for the outline marker.
        view.PreviewScroll.Offset = view.PreviewScroll.Offset.WithY(500);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ActiveHeadingOrdinal > 0, "the active heading should advance from the source position");

        window.Close();
    }

    [AvaloniaFact]
    public void Split_SplitterDrag_FreezesPreviewWidth()
    {
        var (window, view, vm) = OpenScrollableSplit();
        view.SettlePreviewResizeForTest(); // release the freeze that entering split itself caused
        Assert.False(view.PreviewWidthFrozen);

        view.SimulatePreviewResizeForTest(); // a splitter drag changes the preview width
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.PreviewWidthFrozen, "a width change in split must freeze the preview re-wrap");

        view.SettlePreviewResizeForTest();
        Assert.False(view.PreviewWidthFrozen);

        window.Close();
    }
}
