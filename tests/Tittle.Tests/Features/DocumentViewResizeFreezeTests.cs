using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>Guards the resize-freeze fix (A6): Markdown.Avalonia re-measures the whole non-virtualised
/// document on every width change, so while a resize is in flight the preview width is pinned (no
/// re-wrap) and released once on settle. Fence-free sample → no embedded-editor scrollbar (the
/// FluentAvalonia Symbols glyph can't shape headless).</summary>
public class DocumentViewResizeFreezeTests
{
    private const string Sample = "# First\n\nSome prose under the first heading.\n\n## Second\n\nMore prose under the second heading.\n";

    [AvaloniaFact]
    public void Preview_ActiveResize_PinsWidth_AndReleasesOnSettle()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        vm.IsActive = true;
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 1000, Height = 700, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowPreview);
        view.SettlePreviewResizeForTest(); // normalise to a released state
        Assert.False(view.PreviewWidthFrozen);

        view.SimulatePreviewResizeForTest();
        Assert.True(view.PreviewWidthFrozen);  // pinned while the resize is in flight

        view.SettlePreviewResizeForTest();
        Assert.False(view.PreviewWidthFrozen); // released on settle → re-stretches to the viewport

        window.Close();
    }

    [AvaloniaFact]
    public void SourceTab_Resize_DoesNotFreeze()
    {
        // The preview is hidden for a code tab — nothing to pin.
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");
        vm.IsActive = true;
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 1000, Height = 700, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowSource);

        view.SimulatePreviewResizeForTest();
        Assert.False(view.PreviewWidthFrozen);

        window.Close();
    }

    [AvaloniaFact]
    public void FrozenPreview_ExtentChange_DoesNotScheduleReflow()
    {
        // H6: while the preview width is pinned (resize in flight) no re-wrap happens, so the
        // scroll-changed extent deltas must NOT keep restarting the reflow debounce. The pin's
        // release re-lays-out and schedules the single trailing reflow.
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        vm.IsActive = true;
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 1000, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowPreview);
        view.SettlePreviewResizeForTest(); // released baseline

        view.SimulatePreviewResizeForTest();
        Assert.True(view.PreviewWidthFrozen);
        var whileFrozen = view.PreviewReflowScheduleCount;
        for (var i = 0; i < 5; i++)
            view.SimulatePreviewExtentChangeForTest(); // resize-drag scroll frames
        Assert.Equal(whileFrozen, view.PreviewReflowScheduleCount); // none scheduled while pinned

        view.SettlePreviewResizeForTest(); // release the pin
        Assert.False(view.PreviewWidthFrozen);
        view.SimulatePreviewExtentChangeForTest();
        Assert.True(view.PreviewReflowScheduleCount > whileFrozen); // allowed again once released

        window.Close();
    }
}
