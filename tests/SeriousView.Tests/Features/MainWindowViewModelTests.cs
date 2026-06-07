using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateVm(
        string? dialogPath = null, string content = "a\nb\nc", string[]? args = null)
        => new(
            new FakeFileDialogService(dialogPath),
            new FakeFileReader(content),
            new FakeThemeService(),
            new FakeRecentFilesStore(),
            args ?? Array.Empty<string>());

    [AvaloniaFact]
    public void Startup_WithoutArgs_ShowsWelcome_NoTabs()
    {
        var vm = CreateVm();

        Assert.Empty(vm.Tabs);
        Assert.False(vm.HasTabs);
        Assert.Null(vm.SelectedTab);
    }

    [AvaloniaFact]
    public void OpenSample_AddsSampleTab_AndActivatesIt()
    {
        var vm = CreateVm();

        vm.OpenSampleCommand.Execute(null);

        Assert.Single(vm.Tabs);
        Assert.Equal("Пример", vm.SelectedTab!.Header);
    }

    [AvaloniaFact]
    public void Startup_WithFileArg_OpensFileTab()
    {
        var vm = CreateVm(content: "x\ny", args: new[] { "/path/to/sample.cs" });

        Assert.Single(vm.Tabs);
        Assert.Equal("sample.cs", vm.SelectedTab!.Header);
        Assert.Equal(".cs", vm.SelectedTab.GrammarExtension);
        Assert.Equal("x\ny", vm.SelectedTab.DocumentText);
    }

    [AvaloniaFact]
    public async Task OpenFile_AddsTabActivatesIt_AndUpdatesStatus()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md", content: "one\ntwo");

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Single(vm.Tabs);
        Assert.Equal("doc.md", vm.SelectedTab!.Header);
        Assert.Equal(".md", vm.SelectedTab.GrammarExtension);
        // Metrics live on the active tab (right status-bar segment); the window's StatusText
        // is the left "messages" segment.
        Assert.Contains("Строк", vm.SelectedTab.StatusText);
    }

    [AvaloniaFact]
    public async Task OpenFile_WhenCancelled_DoesNotAddTab()
    {
        var vm = CreateVm(dialogPath: null);

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Empty(vm.Tabs);
    }

    [AvaloniaFact]
    public async Task CloseTab_RemovesTab_AndSelectsNeighbour()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md");
        vm.OpenSampleCommand.Execute(null);          // tab 0: sample
        await vm.OpenFileCommand.ExecuteAsync(null); // tab 1: doc.md (active)
        var closing = vm.SelectedTab!;

        vm.CloseTabCommand.Execute(closing);

        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.SelectedTab);
        Assert.NotSame(closing, vm.SelectedTab);
    }

    [AvaloniaFact]
    public void ToggleTheme_CyclesThemeMode_AndUpdatesLabel()
    {
        var theme = new FakeThemeService();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), theme,
            new FakeRecentFilesStore(), Array.Empty<string>());

        Assert.Equal("Тёмная", vm.ThemeModeLabel);

        vm.ToggleThemeCommand.Execute(null);

        Assert.Equal(ThemeMode.Light, theme.Mode);
        Assert.Equal(1, theme.ChangeCount);
        Assert.Equal("Светлая", vm.ThemeModeLabel);
    }

    [AvaloniaFact]
    public void HasTabs_BecomesFalse_AfterClosingLastTab()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);
        Assert.True(vm.HasTabs);

        vm.CloseTabCommand.Execute(vm.SelectedTab);

        Assert.False(vm.HasTabs);
        Assert.Null(vm.SelectedTab);
        Assert.Empty(vm.Tabs);
    }

    [AvaloniaFact]
    public async Task ReorderingTabs_PreservesSelectionAndContent()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md", content: "hello world");
        vm.OpenSampleCommand.Execute(null);          // tab 0: sample
        await vm.OpenFileCommand.ExecuteAsync(null); // tab 1: doc.md selected
        var selected = vm.SelectedTab!;
        var content = selected.DocumentText;

        vm.Tabs.Move(1, 0); // drag the selected tab to the front

        Assert.Same(selected, vm.SelectedTab);               // same instance still selected
        Assert.Equal(content, vm.SelectedTab!.DocumentText); // text (and its editor/TextMate) intact
        Assert.Equal(0, vm.Tabs.IndexOf(selected));
    }

    [AvaloniaFact]
    public async Task OpenFile_RecordsRecentFile()
    {
        var recent = new FakeRecentFilesStore();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/path/doc.md"), new FakeFileReader("x"),
            new FakeThemeService(), recent, Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Contains("/path/doc.md", recent.Items);
        Assert.Contains("/path/doc.md", vm.RecentFiles);
    }

    [AvaloniaFact]
    public void ToggleOutline_FlipsVisibility()
    {
        var vm = CreateVm();

        Assert.True(vm.IsOutlineVisible);
        vm.ToggleOutlineCommand.Execute(null);
        Assert.False(vm.IsOutlineVisible);
        vm.ToggleOutlineCommand.Execute(null);
        Assert.True(vm.IsOutlineVisible);
    }

    [AvaloniaFact]
    public void IsOutlinePaneVisible_RequiresEnabledAndHeadings()
    {
        var vm = CreateVm(content: "# Heading\n\nbody", args: new[] { "/doc.md" });

        Assert.True(vm.IsOutlinePaneVisible);          // markdown + heading + enabled

        vm.ToggleOutlineCommand.Execute(null);
        Assert.False(vm.IsOutlinePaneVisible);         // disabled → hidden
    }

    [AvaloniaFact]
    public void IsOutlinePaneVisible_False_ForCodeFile()
    {
        var vm = CreateVm(content: "var x = 1;", args: new[] { "/a.cs" });

        Assert.True(vm.IsOutlineVisible);              // enabled by default…
        Assert.False(vm.IsOutlinePaneVisible);         // …but no headings → hidden
    }

    [AvaloniaFact]
    public async Task OpenFile_OnReadError_ShowsFriendlyMessage_NoTab()
    {
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/path/missing.txt"),
            new FakeFileReader(new FileNotFoundException()),
            new FakeThemeService(), new FakeRecentFilesStore(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Equal("Файл не найден: missing.txt", vm.StatusText);
        Assert.Empty(vm.Tabs);
    }

    [AvaloniaFact]
    public async Task OpenFile_BinaryFile_OpensNoticeTab()
    {
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/path/image.png"),
            new FakeFileReader(FileLoadResult.Binary(2048)),
            new FakeThemeService(), new FakeRecentFilesStore(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Single(vm.Tabs);
        Assert.True(vm.SelectedTab!.ShowNotice);
        Assert.False(vm.SelectedTab.ShowSource);
    }
}
