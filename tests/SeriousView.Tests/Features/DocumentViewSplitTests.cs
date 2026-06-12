using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeriousView.Core.Settings;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

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
}
