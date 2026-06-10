using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeriousView.Core.Services;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

public class OutlinePanelTests
{
    /// <summary>Shell VM with a markdown tab (3 headings) opened at startup via the file arg.</summary>
    private static MainWindowViewModel CreateShellVm(params string[] args)
        => new(
            new FakeFileDialogService(null),
            new FakeFileReader("# A\n\ntext\n\n## B\n\ntext\n\n## C\n\ntext"),
            new FakeThemeService(), new FakeRecentFilesStore(),
            new AppSettingsService(new FakeSettingsStore()),
            new FakeClipboardService(), new FakeShellService(), args);

    private static List<Border> ActiveMarkersOf(Window window)
        => window.GetVisualDescendants().OfType<Border>()
            .Where(b => b.Classes.Contains("active-marker")).ToList();

    [AvaloniaFact]
    public void Outline_MarksTheActiveHeading_AndFollowsIt()
    {
        var vm = CreateShellVm("/docs/doc.md");
        var window = new Window { Width = 300, Height = 400, Content = new OutlinePanel { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.SelectedTab!.ActiveHeadingOrdinal = 1;
        Dispatcher.UIThread.RunJobs();

        var markers = ActiveMarkersOf(window);
        Assert.Equal(3, markers.Count); // one reserved per item, only the active one visible
        Assert.Equal(new[] { false, true, false }, markers.Select(m => m.IsVisible));

        vm.SelectedTab.ActiveHeadingOrdinal = 2;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new[] { false, false, true }, ActiveMarkersOf(window).Select(m => m.IsVisible));
        window.Close();
    }

    [AvaloniaFact]
    public void Outline_NoSelectedTab_ShowsNoMarker_AndDoesNotThrow()
    {
        var vm = CreateShellVm(); // no args → welcome, SelectedTab is null
        var window = new Window { Width = 300, Height = 400, Content = new OutlinePanel { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(ActiveMarkersOf(window), m => m.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void OutlinePanel_ScrollbarAutoHides_LikeTheRestOfTheChrome()
    {
        // The slim auto-hide scrollbar matches the markdown preview's (consistency). The earlier
        // "hover compression" was FluentAvalonia's button template — fixed by the custom outline-item
        // template — not the scrollbar, so the outline no longer pins AllowAutoHide=False.
        var window = new Window { Content = new OutlinePanel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.AllowAutoHide);

        window.Close();
    }
}
