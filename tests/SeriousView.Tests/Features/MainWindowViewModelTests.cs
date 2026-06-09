using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Services;
using SeriousView.Core.Settings;
using SeriousView.Features.Shell;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

public class MainWindowViewModelTests
{
    private static IAppSettingsService Holder(AppSettings? seed = null)
    {
        var store = new FakeSettingsStore();
        var holder = new AppSettingsService(store);
        if (seed is not null)
            holder.Update(seed);
        return holder;
    }

    private static MainWindowViewModel CreateVm(
        string? dialogPath = null, string content = "a\nb\nc", string[]? args = null,
        IFileReader? fileReader = null, IAppSettingsService? settings = null,
        IClipboardService? clipboard = null)
        => new(
            new FakeFileDialogService(dialogPath),
            fileReader ?? new FakeFileReader(content),
            new FakeThemeService(),
            new FakeRecentFilesStore(),
            settings ?? Holder(),
            clipboard ?? new FakeClipboardService(),
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
    public void Welcome_StatusBar_ShowsHint_NotReady()
    {
        var vm = CreateVm();

        Assert.Contains("Откройте файл", vm.StatusText); // actionable hint, not a bare "Готово"
        Assert.DoesNotContain("Готово", vm.StatusText);
    }

    [AvaloniaFact]
    public void OpeningTab_ClearsLeftStatus_ThenHintReturnsOnClose()
    {
        var vm = CreateVm(content: "x", args: new[] { "/a.cs" }); // opens a tab at startup

        Assert.Equal("", vm.StatusText); // left segment cleared while a tab is active

        vm.CloseActiveTabCommand.Execute(null);

        Assert.Contains("Откройте файл", vm.StatusText); // welcome hint restored on the empty screen
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
    public void ToggleTheme_CyclesThemeMode_AndUpdatesCurrent()
    {
        var theme = new FakeThemeService();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), theme,
            new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), Array.Empty<string>());

        Assert.Equal(ThemeMode.Dark, vm.CurrentTheme);

        vm.ToggleThemeCommand.Execute(null);

        Assert.Equal(ThemeMode.Light, theme.Mode);
        Assert.Equal(1, theme.ChangeCount);
        Assert.Equal(ThemeMode.Light, vm.CurrentTheme);
    }

    [AvaloniaFact]
    public void SetTheme_AppliesModeDirectly_AndUpdatesCurrent()
    {
        var theme = new FakeThemeService(); // starts Dark
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), theme,
            new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), Array.Empty<string>());

        vm.SetThemeCommand.Execute(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, theme.Mode);
        Assert.Equal(ThemeMode.Light, vm.CurrentTheme);
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
    public async Task OpenPath_SameFileTwice_ReusesTab_NoDuplicate()
    {
        var vm = CreateVm(content: "x");
        await vm.OpenPathAsync("/docs/readme.md");
        var first = vm.SelectedTab!;
        vm.OpenSampleCommand.Execute(null); // move the selection away

        await vm.OpenPathAsync("/docs/readme.md"); // reopen the same file

        Assert.Equal(2, vm.Tabs.Count);     // sample + readme — no second readme tab
        Assert.Same(first, vm.SelectedTab); // the existing tab is re-activated
    }

    [AvaloniaFact]
    public async Task OpenPath_SameFile_DifferentCase_ReusesTab()
    {
        var vm = CreateVm(content: "x");
        await vm.OpenPathAsync("/docs/readme.md");
        var first = vm.SelectedTab!;

        await vm.OpenPathAsync("/docs/README.MD"); // same file, different case

        Assert.Single(vm.Tabs);
        Assert.Same(first, vm.SelectedTab);
    }

    [AvaloniaFact]
    public async Task OpenPath_DifferentFiles_OpenSeparateTabs()
    {
        var vm = CreateVm(content: "x");
        await vm.OpenPathAsync("/docs/a.md");
        await vm.OpenPathAsync("/docs/b.md");

        Assert.Equal(2, vm.Tabs.Count);
    }

    [AvaloniaFact]
    public async Task OpenFile_RecordsRecentFile()
    {
        var recent = new FakeRecentFilesStore();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/path/doc.md"), new FakeFileReader("x"),
            new FakeThemeService(), recent, Holder(), new FakeClipboardService(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Contains("/path/doc.md", recent.Items);
    }

    [AvaloniaFact]
    public void RecentItems_ProjectPaths_IntoNameAndFolder_WithOpenCommand()
    {
        var recent = new FakeRecentFilesStore();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), new FakeThemeService(),
            recent, Holder(), new FakeClipboardService(), Array.Empty<string>());
        var dir = Path.Combine(Path.GetTempPath(), "docs");
        var path = Path.Combine(dir, "readme.md");

        recent.Add(path);

        var item = Assert.Single(vm.RecentItems);
        Assert.Equal("readme.md", item.Name);
        Assert.Equal(dir, item.Folder);
        Assert.Equal(path, item.Path);
        Assert.NotNull(item.OpenCommand);
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
            new FakeThemeService(), new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), Array.Empty<string>());

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
            new FakeThemeService(), new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Single(vm.Tabs);
        Assert.True(vm.SelectedTab!.ShowNotice);
        Assert.False(vm.SelectedTab.ShowSource);
    }

    [AvaloniaFact]
    public void Startup_WithSession_RestoresTabs_AndSelectsSavedActive()
    {
        var files = new Dictionary<string, string> { ["/a.md"] = "# A", ["/b.md"] = "# B" };
        var settings = Holder(new AppSettings { Session = new SessionState(new() { "/a.md", "/b.md" }, 1) });

        var vm = CreateVm(fileReader: new FakeFileReader(files), settings: settings);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal("b.md", vm.SelectedTab!.Header); // ActiveIndex 1
    }

    [AvaloniaFact]
    public void Startup_Session_SkipsMissingFiles()
    {
        var files = new Dictionary<string, string> { ["/a.md"] = "# A", ["/b.md"] = "# B" };
        var settings = Holder(new AppSettings
        {
            Session = new SessionState(new() { "/a.md", "/gone.md", "/b.md" }, 0),
        });

        var vm = CreateVm(fileReader: new FakeFileReader(files), settings: settings);

        Assert.Equal(2, vm.Tabs.Count); // the missing file is silently skipped
        Assert.Equal("a.md", vm.SelectedTab!.Header);
    }

    [AvaloniaFact]
    public void Startup_FileArg_BeatsSession()
    {
        var settings = Holder(new AppSettings { Session = new SessionState(new() { "/a.md", "/b.md" }, 0) });

        var vm = CreateVm(content: "x", args: new[] { "/arg.cs" }, settings: settings);

        Assert.Single(vm.Tabs);                       // the session is ignored when a file arg is given
        Assert.Equal("arg.cs", vm.SelectedTab!.Header);
    }

    [AvaloniaFact]
    public async Task GetSession_SnapshotsOpenFilePaths_AndActiveIndex()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md", content: "x");
        vm.OpenSampleCommand.Execute(null);          // sample tab (no FilePath) — excluded
        await vm.OpenFileCommand.ExecuteAsync(null); // /path/doc.md, active

        var session = vm.GetSession();

        Assert.Equal(new[] { "/path/doc.md" }, session.OpenFiles); // only file-backed tabs
        Assert.Equal(0, session.ActiveIndex);                      // doc.md is index 0 among them
    }

    [AvaloniaFact]
    public void ZoomIn_ChangesEditorFont_AndPersists()
    {
        var holder = Holder();
        var vm = CreateVm(settings: holder);

        vm.ZoomInCommand.Execute(null);
        Assert.Equal(EditorOptions.DefaultFontSize + 1, vm.Editor.FontSize); // in-memory: immediate

        // Persistence is debounced (a zoom burst coalesces into one write); flush lands it.
        vm.FlushEditorSettings();
        Assert.Equal(EditorOptions.DefaultFontSize + 1, holder.Current.Editor!.FontSize); // persisted
    }

    [AvaloniaFact]
    public void SelectingTab_ActivatesExactlyOne()
    {
        // The shell keeps every tab's view alive and shows only the active one (IsActive), so exactly
        // one tab must be active and it must follow the selection.
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // tab 0
        vm.OpenSampleCommand.Execute(null); // tab 1 (selected)
        var (t0, t1) = (vm.Tabs[0], vm.Tabs[1]);

        Assert.False(t0.IsActive);
        Assert.True(t1.IsActive);

        vm.SelectedTab = t0;
        Assert.True(t0.IsActive);
        Assert.False(t1.IsActive);
    }

    [AvaloniaFact]
    public void Editor_IsRestoredFromSettings()
    {
        var holder = Holder(new AppSettings { Editor = new EditorSettings(20, WordWrap: true, ShowLineNumbers: false) });
        var vm = CreateVm(settings: holder);

        Assert.Equal(20, vm.Editor.FontSize);
        Assert.True(vm.Editor.WordWrap);
        Assert.False(vm.Editor.ShowLineNumbers);
    }

    [AvaloniaFact]
    public void AllTabs_ShareTheSameEditorOptions()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);
        vm.OpenSampleCommand.Execute(null);

        Assert.Same(vm.Editor, vm.Tabs[0].Editor);
        Assert.Same(vm.Editor, vm.Tabs[1].Editor);
    }

    [AvaloniaFact]
    public void AllTabs_ShareTheSameLayoutOptions()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);

        Assert.Same(vm.Layout, vm.Tabs[0].Layout);
    }

    [AvaloniaFact]
    public void ToggleReadingMode_Flips_AndPersists()
    {
        var holder = Holder();
        var vm = CreateVm(settings: holder);

        Assert.True(vm.Layout.ReadingMode); // on by default

        vm.ToggleReadingModeCommand.Execute(null);

        Assert.False(vm.Layout.ReadingMode);
        Assert.False(holder.Current.Layout!.ReadingMode); // persisted via Layout.PropertyChanged
    }

    [AvaloniaFact]
    public void SelectNextTab_CyclesForward_WithWrap()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // tab 0
        vm.OpenSampleCommand.Execute(null); // tab 1 (active)
        var (t0, t1) = (vm.Tabs[0], vm.Tabs[1]);

        vm.SelectNextTabCommand.Execute(null); // wraps to tab 0
        Assert.Same(t0, vm.SelectedTab);
        vm.SelectNextTabCommand.Execute(null); // tab 1
        Assert.Same(t1, vm.SelectedTab);
    }

    [AvaloniaFact]
    public void SelectPreviousTab_CyclesBackward_WithWrap()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // tab 0
        vm.OpenSampleCommand.Execute(null); // tab 1 (active)
        var (t0, t1) = (vm.Tabs[0], vm.Tabs[1]);

        vm.SelectPreviousTabCommand.Execute(null); // tab 0
        Assert.Same(t0, vm.SelectedTab);
        vm.SelectPreviousTabCommand.Execute(null); // wraps to tab 1
        Assert.Same(t1, vm.SelectedTab);
    }

    [AvaloniaFact]
    public void CycleTab_WithNoTabs_IsNoOp()
    {
        var vm = CreateVm();

        vm.SelectNextTabCommand.Execute(null);

        Assert.Null(vm.SelectedTab);
        Assert.Empty(vm.Tabs);
    }

    [AvaloniaFact]
    public void CloseActiveTab_ClosesTheSelectedTab()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);
        vm.OpenSampleCommand.Execute(null);

        vm.CloseActiveTabCommand.Execute(null);

        Assert.Single(vm.Tabs);
    }

    [AvaloniaFact]
    public void AddedTab_GetsShellBackReference()
    {
        var vm = CreateVm();

        vm.OpenSampleCommand.Execute(null);

        Assert.Same(vm, vm.Tabs[0].Shell); // wired in AddTab so the context menu can reach the shell
    }

    [AvaloniaFact]
    public void CloseOtherTabs_KeepsOnlyTheTarget_AndSelectsIt()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // 0
        vm.OpenSampleCommand.Execute(null); // 1
        vm.OpenSampleCommand.Execute(null); // 2
        var keep = vm.Tabs[1];

        vm.CloseOtherTabsCommand.Execute(keep);

        Assert.Single(vm.Tabs);
        Assert.Same(keep, vm.Tabs[0]);
        Assert.Same(keep, vm.SelectedTab);
    }

    [AvaloniaFact]
    public void CloseTabsToRight_RemovesTabsAfterTarget_AndFixesSelection()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // 0
        vm.OpenSampleCommand.Execute(null); // 1
        vm.OpenSampleCommand.Execute(null); // 2 (selected)
        var pivot = vm.Tabs[0];

        vm.CloseTabsToRightCommand.Execute(pivot);

        Assert.Single(vm.Tabs);             // only the pivot remains
        Assert.Same(pivot, vm.Tabs[0]);
        Assert.Same(pivot, vm.SelectedTab); // the removed selection fell back to the pivot
    }

    [AvaloniaFact]
    public void CloseTabsToRight_KeepsSelection_WhenSelectedIsLeftOfPivot()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // 0
        vm.OpenSampleCommand.Execute(null); // 1
        vm.OpenSampleCommand.Execute(null); // 2
        var left = vm.Tabs[0];
        vm.SelectedTab = left;
        var pivot = vm.Tabs[1];

        vm.CloseTabsToRightCommand.Execute(pivot);

        Assert.Equal(2, vm.Tabs.Count);    // 0 and 1 remain, 2 removed
        Assert.Same(left, vm.SelectedTab); // selection (left of the pivot) is untouched
    }

    [AvaloniaFact]
    public void CloseAllTabs_EmptiesAndDeselects()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);
        vm.OpenSampleCommand.Execute(null);

        vm.CloseAllTabsCommand.Execute(null);

        Assert.Empty(vm.Tabs);
        Assert.Null(vm.SelectedTab);
        Assert.False(vm.HasTabs);
    }

    [AvaloniaFact]
    public async Task CopyFilePath_PutsFullPathOnClipboard()
    {
        var clip = new FakeClipboardService();
        var vm = CreateVm(content: "x", clipboard: clip);
        await vm.OpenPathAsync("/docs/readme.md");

        await vm.CopyFilePathCommand.ExecuteAsync(vm.SelectedTab);

        Assert.Equal("/docs/readme.md", clip.LastText);
    }

    [AvaloniaFact]
    public async Task CopyFileName_PutsFileNameOnClipboard()
    {
        var clip = new FakeClipboardService();
        var vm = CreateVm(content: "x", clipboard: clip);
        await vm.OpenPathAsync("/docs/readme.md");

        await vm.CopyFileNameCommand.ExecuteAsync(vm.SelectedTab);

        Assert.Equal("readme.md", clip.LastText);
    }

    [AvaloniaFact]
    public async Task Copy_NoOp_WhenTabHasNoPath()
    {
        var clip = new FakeClipboardService();
        var vm = CreateVm(clipboard: clip);
        vm.OpenSampleCommand.Execute(null); // the sample tab has no FilePath

        await vm.CopyFilePathCommand.ExecuteAsync(vm.SelectedTab);

        Assert.Null(clip.LastText); // nothing copied
    }

    [AvaloniaFact]
    public void OpenGoToLine_OpensOverlay_OnSourceTab()
    {
        var vm = CreateVm(content: "x", args: new[] { "/a.cs" }); // code → source view

        vm.OpenGoToLineCommand.Execute(null);

        Assert.True(vm.SelectedTab!.IsGoToLineOpen);
    }

    [AvaloniaFact]
    public void OpenGoToLine_NoOp_ForMarkdownPreview()
    {
        var vm = CreateVm(content: "# H", args: new[] { "/a.md" }); // markdown → preview

        vm.OpenGoToLineCommand.Execute(null);

        Assert.False(vm.SelectedTab!.IsGoToLineOpen);
    }

    [AvaloniaFact]
    public void OpenGoToLine_NoOp_WhenNoTab()
    {
        var vm = CreateVm();

        vm.OpenGoToLineCommand.Execute(null); // welcome, no selected tab — must not throw

        Assert.Null(vm.SelectedTab);
    }
}
