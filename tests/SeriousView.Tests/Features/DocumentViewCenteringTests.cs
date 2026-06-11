using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SeriousView.Core.Settings;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

/// <summary>Guards that the reading-column presets actually CENTER the preview column, not merely that
/// the converter returns <c>HorizontalAlignment.Center</c>. A ScrollViewer's ScrollContentPresenter
/// ignores its direct child's alignment (arranges it at the left edge), so Comfort/Narrow used to
/// render left-aligned with all the slack on the right; the fix wraps the preview in a stretching
/// Panel. We assert the real laid-out geometry. Fence-free, short sample → no embedded-editor /
/// vertical scrollbar (the FluentAvalonia Symbols glyph can't shape headless).</summary>
public class DocumentViewCenteringTests
{
    private const string Sample = "# First\n\nSome prose under the first heading.\n\n## Second\n\nMore prose here.\n";

    [AvaloniaFact]
    public void ComfortPreset_CentersColumn_LeavingEqualLeftAndRightSlack()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        vm.IsActive = true;
        vm.Layout = new LayoutOptions { ReadingWidth = ReadingWidth.Comfort };
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 1600, Height = 700, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowPreview);

        // Capped at the Comfort column width...
        Assert.True(view.PreviewColumnWidthForTest <= ReadingWidthConverter.ComfortWidth + 1,
            $"width was {view.PreviewColumnWidthForTest}");
        // ...and genuinely centered: a real left gap, not flush-left (which would be ~0).
        Assert.True(view.PreviewColumnLeftOffsetForTest > 50,
            $"left offset was {view.PreviewColumnLeftOffsetForTest} (expected a centering gap)");

        window.Close();
    }

    [AvaloniaFact]
    public void FullPreset_FillsWidth_FlushLeft()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        vm.IsActive = true;
        vm.Layout = new LayoutOptions { ReadingWidth = ReadingWidth.Full };
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 1600, Height = 700, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(vm.ShowPreview);

        // Full stretches to the viewport: no centering gap and wider than any preset column.
        Assert.True(view.PreviewColumnLeftOffsetForTest < 2,
            $"left offset was {view.PreviewColumnLeftOffsetForTest} (Full should be flush-left)");
        Assert.True(view.PreviewColumnWidthForTest > ReadingWidthConverter.ComfortWidth,
            $"width was {view.PreviewColumnWidthForTest} (Full should exceed the Comfort cap)");

        window.Close();
    }
}
