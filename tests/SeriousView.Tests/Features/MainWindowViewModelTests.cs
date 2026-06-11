using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Services;
using SeriousView.Core.Settings;
using SeriousView.Core.Text;
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
        IClipboardService? clipboard = null, IShellService? shell = null,
        IDocumentWatcher? watcher = null, ViewStateStore? viewState = null)
        => new(
            new FakeFileDialogService(dialogPath),
            fileReader ?? new FakeFileReader(content),
            new FakeThemeService(),
            new FakeRecentFilesStore(),
            settings ?? Holder(),
            clipboard ?? new FakeClipboardService(),
            shell ?? new FakeShellService(),
            args ?? Array.Empty<string>(),
            watcher,
            viewState);

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
            new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

        Assert.Equal(ThemeMode.Dark, vm.CurrentTheme);

        vm.ToggleThemeCommand.Execute(null);

        // The cycle now walks the dark set first (ported DARK_THEMES).
        Assert.Equal(ThemeMode.Midnight, theme.Mode);
        Assert.Equal(1, theme.ChangeCount);
        Assert.Equal(ThemeMode.Midnight, vm.CurrentTheme);
    }

    [AvaloniaFact]
    public void SetTheme_AppliesModeDirectly_AndUpdatesCurrent()
    {
        var theme = new FakeThemeService(); // starts Dark
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), theme,
            new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

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
    public async Task OpenFile_MultiplePicked_OpensTabPerFile()
    {
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/docs/a.md", "/docs/b.md"), new FakeFileReader("x"),
            new FakeThemeService(), new FakeRecentFilesStore(), Holder(),
            new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal("/docs/b.md", vm.SelectedTab?.FilePath); // the last picked file ends up active
    }

    [AvaloniaFact]
    public async Task OpenFile_RecordsRecentFile()
    {
        var recent = new FakeRecentFilesStore();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService("/path/doc.md"), new FakeFileReader("x"),
            new FakeThemeService(), recent, Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Contains("/path/doc.md", recent.Items);
    }

    [AvaloniaFact]
    public void RecentItems_ProjectPaths_IntoNameAndFolder_WithOpenCommand()
    {
        var recent = new FakeRecentFilesStore();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), new FakeThemeService(),
            recent, Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());
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
            new FakeThemeService(), new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

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
            new FakeThemeService(), new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

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
    public async Task OpenFile_Error_ShowsErrorBar_AndStatusText()
    {
        var vm = CreateVm(dialogPath: "/path/missing.txt",
            fileReader: new FakeFileReader(new FileNotFoundException()));

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.True(vm.IsErrorBarOpen);
        Assert.Equal("Файл не найден: missing.txt", vm.ErrorBarMessage);
        Assert.Equal("Файл не найден: missing.txt", vm.StatusText); // the status bar keeps duplicating
    }

    [AvaloniaFact]
    public async Task ErrorBar_AutoDismisses_AfterDelay()
    {
        var vm = CreateVm(dialogPath: "/path/missing.txt",
            fileReader: new FakeFileReader(new FileNotFoundException()));
        vm.ErrorBarAutoDismissDelay = TimeSpan.FromMilliseconds(30);

        await vm.OpenFileCommand.ExecuteAsync(null);
        Assert.True(vm.IsErrorBarOpen);

        await vm.ErrorBarDismissal!;
        Assert.False(vm.IsErrorBarOpen);
    }

    [AvaloniaFact]
    public async Task ErrorBar_NewError_ReplacesMessage_AndOutlivesTheOldTimer()
    {
        var vm = CreateVm(fileReader: new FakeFileReader(new FileNotFoundException()));

        await vm.OpenPathAsync("/one.txt");
        var firstDismissal = vm.ErrorBarDismissal;
        await vm.OpenPathAsync("/two.txt");

        Assert.True(vm.IsErrorBarOpen);
        Assert.Contains("two.txt", vm.ErrorBarMessage);

        await firstDismissal!; // the superseded timer must not close the newer message
        Assert.True(vm.IsErrorBarOpen);
    }

    [AvaloniaFact]
    public void Startup_Session_MissingFiles_SurfaceOneSummaryError()
    {
        var files = new Dictionary<string, string> { ["/a.md"] = "# A" };
        var settings = Holder(new AppSettings
        {
            Session = new SessionState(new() { "/a.md", "/gone.md", "/lost.md" }, 0),
        });

        var vm = CreateVm(fileReader: new FakeFileReader(files), settings: settings);

        Assert.Single(vm.Tabs); // restore still skips the missing tabs...
        Assert.True(vm.IsErrorBarOpen); // ...but no longer silently
        Assert.Contains("gone.md", vm.ErrorBarMessage);
        Assert.Contains("lost.md", vm.ErrorBarMessage);
    }

    // --- M14: external-change watching (C1 — watch lifecycle + the dirty dot) ---

    [AvaloniaFact]
    public async Task Watcher_FileBackedTabsAreWatched_TheSampleIsNot()
    {
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(content: "x", args: new[] { "/docs/a.md" }, watcher: watcher);

        Assert.Equal(new[] { "/docs/a.md" }, watcher.Watched);

        vm.OpenSampleCommand.Execute(null); // path-less tab — nothing to watch
        Assert.Equal(new[] { "/docs/a.md" }, watcher.Watched);

        await vm.OpenPathAsync("/docs/b.md");
        Assert.Equal(new[] { "/docs/a.md", "/docs/b.md" }, watcher.Watched);
    }

    [AvaloniaFact]
    public async Task Watcher_ClosingTabs_Unwatches()
    {
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(content: "x", watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        await vm.OpenPathAsync("/docs/b.md");

        vm.CloseTabCommand.Execute(vm.Tabs[0]);
        Assert.Equal(new[] { "/docs/b.md" }, watcher.Watched);

        vm.CloseAllTabsCommand.Execute(null);
        Assert.Empty(watcher.Watched);
    }

    [AvaloniaFact]
    public async Task Watcher_Changed_DotsAnInactiveTab()
    {
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(content: "x", watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        await vm.OpenPathAsync("/docs/b.md"); // b is active, a inactive
        Assert.False(vm.Tabs[0].IsChangedOnDisk);

        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs(); // the handler hops to the UI thread

        Assert.True(vm.Tabs[0].IsChangedOnDisk);
    }

    [AvaloniaFact]
    public async Task Watcher_Removed_DotPlusError_TabAndContentKept()
    {
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(content: "keep me", watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");

        watcher.Raise("/docs/a.md", DocumentChangeKind.Removed);
        Dispatcher.UIThread.RunJobs();

        var tab = Assert.Single(vm.Tabs);            // the tab is NOT closed
        Assert.True(tab.IsChangedOnDisk);
        Assert.Equal("keep me", tab.DocumentText);   // last-loaded content stays visible
        Assert.True(vm.IsErrorBarOpen);
        Assert.Contains("удал", vm.ErrorBarMessage);
    }

    [AvaloniaFact]
    public void Watcher_EventForAnUnknownPath_IsANoOp()
    {
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(watcher: watcher);

        watcher.Raise("/docs/ghost.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.IsErrorBarOpen);
        Assert.Empty(vm.Tabs);
    }

    [AvaloniaFact]
    public async Task Palette_OffersReload_OnlyForFileBackedTabs()
    {
        var vm = CreateVm(content: "x");

        vm.OpenSampleCommand.Execute(null);
        Assert.DoesNotContain(vm.BuildPaletteItems(), i => i.Title.Contains("Перезагрузить с диска"));

        await vm.OpenPathAsync("/docs/a.md");
        Assert.Contains(vm.BuildPaletteItems(), i => i.Title.Contains("Перезагрузить с диска"));
    }

    // --- M14: reload (C2 — auto for the active tab, manual command elsewhere) ---

    [AvaloniaFact]
    public async Task ActiveTab_ExternalChange_AutoReloadsInPlace()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "# old", ["/docs/b.md"] = "x" };
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: new FakeFileReader(map), watcher: watcher);
        await vm.OpenPathAsync("/docs/b.md");
        await vm.OpenPathAsync("/docs/a.md"); // index 1, active
        vm.SelectedTab!.ViewMode = DocumentViewMode.Source;

        map["/docs/a.md"] = "# new";
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();
        await vm.PendingReload!;

        Assert.Equal(2, vm.Tabs.Count);
        Assert.NotNull(vm.SelectedTab); // the replace must not drop the ListBox selection
        Assert.Same(vm.Tabs[1], vm.SelectedTab);
        Assert.Equal("# new", vm.SelectedTab!.DocumentText);
        Assert.Equal(DocumentViewMode.Source, vm.SelectedTab.ViewMode); // mode survives
        Assert.False(vm.SelectedTab.IsChangedOnDisk);
        Assert.Contains("обновлён", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task InactiveTab_ExternalChange_OnlyDots_ManualCommandReloads()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "# old", ["/docs/b.md"] = "x" };
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: new FakeFileReader(map), watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        await vm.OpenPathAsync("/docs/b.md"); // b active, a inactive

        map["/docs/a.md"] = "# new";
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();

        var inactive = vm.Tabs[0];
        Assert.True(inactive.IsChangedOnDisk);
        Assert.Equal("# old", inactive.DocumentText); // manual policy: no reload yet
        Assert.Null(vm.PendingReload);

        vm.ReloadTabCommand.Execute(inactive);
        await vm.PendingReload!;

        Assert.Equal("# new", vm.Tabs[0].DocumentText);
        Assert.False(vm.Tabs[0].IsChangedOnDisk);
        Assert.Same(vm.Tabs[1], vm.SelectedTab); // reloading an inactive tab keeps the selection
    }

    [AvaloniaFact]
    public async Task Reload_Failure_KeepsTheTabAndContent_ShowsTheError()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "keep" };
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: new FakeFileReader(map), watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");

        map.Remove("/docs/a.md"); // the file is gone by reload time
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();
        await vm.PendingReload!;

        var tab = Assert.Single(vm.Tabs);
        Assert.Equal("keep", tab.DocumentText);
        Assert.True(tab.IsChangedOnDisk); // the dot stays — disk still differs
        Assert.True(vm.IsErrorBarOpen);
    }

    [AvaloniaFact]
    public async Task Reload_TransientIOException_RetriesOnce()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "# new" };
        var reader = new FakeFileReader(map);
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: reader, watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        vm.ReloadRetryDelay = TimeSpan.Zero;

        reader.FailNextLoads = 1; // the editor still holds the file on the first attempt
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();
        await vm.PendingReload!;

        Assert.Equal("# new", vm.Tabs[0].DocumentText);
        Assert.False(vm.IsErrorBarOpen);
    }

    [AvaloniaFact]
    public async Task Reload_HandsTheReadingAnchorToTheFreshTab()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "# old" };
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: new FakeFileReader(map), watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        vm.Tabs[0].ReadingAnchor = new HeadingAnchor(1, 0.5); // written by the view in real life

        map["/docs/a.md"] = "# new";
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();
        await vm.PendingReload!;

        Assert.Equal(new HeadingAnchor(1, 0.5), vm.Tabs[0].RestoreAnchor);
    }

    [AvaloniaFact]
    public async Task Reload_KeepsTheSessionSnapshotStable()
    {
        var map = new Dictionary<string, string> { ["/docs/a.md"] = "old", ["/docs/b.md"] = "x" };
        var watcher = new FakeDocumentWatcher();
        var vm = CreateVm(fileReader: new FakeFileReader(map), watcher: watcher);
        await vm.OpenPathAsync("/docs/a.md");
        await vm.OpenPathAsync("/docs/b.md");
        vm.SelectedTab = vm.Tabs[0];

        map["/docs/a.md"] = "new";
        watcher.Raise("/docs/a.md", DocumentChangeKind.Changed);
        Dispatcher.UIThread.RunJobs();
        await vm.PendingReload!;

        var session = vm.GetSession();
        Assert.Equal(new[] { "/docs/a.md", "/docs/b.md" }, session.OpenFiles);
        Assert.Equal(0, session.ActiveIndex);
    }

    // --- Ported: code-symbol and plain-text outlines feed the shared TOC machinery ---

    [AvaloniaFact]
    public async Task CodeTab_GetsASymbolOutline()
    {
        var vm = CreateVm(content: "public class Widget\n{\n    public void Run() { }\n}");

        await vm.OpenPathAsync("/src/widget.cs");

        Assert.True(vm.SelectedTab!.HasOutline);
        Assert.Equal(new[] { "Widget", "Run" },
            System.Linq.Enumerable.Select(vm.SelectedTab.Outline, h => h.Text));
    }

    [AvaloniaFact]
    public async Task TextTab_GetsAPlainTextOutline()
    {
        var vm = CreateVm(content: "Глава 1. Начало\nтекст\n==== Финал ====");

        await vm.OpenPathAsync("/docs/story.txt");

        Assert.Equal(new[] { "Глава 1. Начало", "Финал" },
            System.Linq.Enumerable.Select(vm.SelectedTab!.Outline, h => h.Text));
    }

    // --- Ported: settings import/export ---

    [AvaloniaFact]
    public async Task SettingsRoundTrip_ExportThenImport_AppliesLive()
    {
        var dir = Directory.CreateTempSubdirectory("sv-settings-").FullName;
        try
        {
            var file = Path.Combine(dir, "s.json");
            var source = CreateVm(settings: Holder(new AppSettings
            {
                Theme = ThemeMode.Light,
                Editor = new EditorSettings(20, true, false),
            }));
            var sourceDialog = new FakeFileDialogService(null) { SavePath = file };
            source = new MainWindowViewModel(sourceDialog, new FakeFileReader("x"),
                new FakeThemeService(), new FakeRecentFilesStore(),
                Holder(new AppSettings { Theme = ThemeMode.Light, Editor = new EditorSettings(20, true, false) }),
                new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

            await source.ExportSettingsCommand.ExecuteAsync(null);
            Assert.Contains("\"Theme\"", File.ReadAllText(file));

            var theme = new FakeThemeService();
            var target = new MainWindowViewModel(new FakeFileDialogService(file),
                new FakeFileReader("x"), theme, new FakeRecentFilesStore(), Holder(),
                new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

            await target.ImportSettingsCommand.ExecuteAsync(null);

            Assert.Equal(ThemeMode.Light, theme.Mode);
            Assert.Equal(20, target.Editor.FontSize);
            Assert.True(target.Editor.WordWrap);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task ImportSettings_GarbageFile_ShowsAnError()
    {
        var dir = Directory.CreateTempSubdirectory("sv-settings-bad-").FullName;
        try
        {
            var file = Path.Combine(dir, "bad.json");
            File.WriteAllText(file, "{not json");
            var vm = new MainWindowViewModel(new FakeFileDialogService(file),
                new FakeFileReader("x"), new FakeThemeService(), new FakeRecentFilesStore(),
                Holder(), new FakeClipboardService(), new FakeShellService(), Array.Empty<string>());

            await vm.ImportSettingsCommand.ExecuteAsync(null);

            Assert.True(vm.IsErrorBarOpen);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // --- Ported: document statistics ---

    [AvaloniaFact]
    public async Task ShowStats_RaisesComputedStatsForTheActiveTab()
    {
        var vm = CreateVm(content: "Раз два три. Четыре пять!");
        await vm.OpenPathAsync("/docs/a.md");
        TextStats? received = null;
        vm.StatsRequested += s => received = s;

        vm.ShowStatsCommand.Execute(null);

        Assert.NotNull(received);
        Assert.Equal(5, received!.Words);
        Assert.Equal(2, received.Sentences);
    }

    // --- Ported: smart typography for plain text (display-only) ---

    [AvaloniaFact]
    public async Task TextTab_GetsSmartTypography_ByDefault_RawTextUntouched()
    {
        var vm = CreateVm(content: "идея -- хорошая...");
        await vm.OpenPathAsync("/docs/notes.txt");

        Assert.Equal("идея — хорошая…", vm.SelectedTab!.SourceText);
        Assert.Equal("идея -- хорошая...", vm.SelectedTab.DocumentText);

        vm.SelectedTab.ToggleSmartTypographyCommand.Execute(null);
        Assert.Equal("идея -- хорошая...", vm.SelectedTab.SourceText);
        Assert.False(vm.Editor.SmartTypography); // persisted default flipped
    }

    // --- Ported: CSV/TSV as a sortable table ---

    [AvaloniaFact]
    public async Task CsvTab_ShowsTheTableByDefault_ToggleFallsBackToSource()
    {
        var vm = CreateVm(content: "name,age\nАня,30\nБорис,25");
        await vm.OpenPathAsync("/data/people.csv");

        var tab = vm.SelectedTab!;
        Assert.True(tab.ShowCsvTable);
        Assert.False(tab.ShowSource);
        Assert.Equal(new[] { "name", "age" },
            System.Linq.Enumerable.Select(tab.CsvTable!.Columns, c => c.Header));

        tab.ToggleCsvViewCommand.Execute(null);

        Assert.False(tab.ShowCsvTable);
        Assert.True(tab.ShowSource);
        Assert.False(vm.Editor.CsvAsTable); // becomes the persisted default
    }

    [AvaloniaFact]
    public async Task CsvTable_SortsNumerically_AndReverses()
    {
        var vm = CreateVm(content: "name,age\nАня,30\nБорис,9\nВера,100");
        await vm.OpenPathAsync("/data/people.csv");
        var table = vm.SelectedTab!.CsvTable!;
        var ageColumn = table.Columns[1];

        table.SortByCommand.Execute(ageColumn);
        Assert.Equal(new[] { "9", "30", "100" },
            System.Linq.Enumerable.Select(table.Rows, r => r.Cells[1].Text)); // numeric, not "100"<"30"

        table.SortByCommand.Execute(ageColumn);
        Assert.Equal(new[] { "100", "30", "9" },
            System.Linq.Enumerable.Select(table.Rows, r => r.Cells[1].Text)); // second click reverses
    }

    [AvaloniaFact]
    public async Task BrokenCsv_FallsBackToTheSourceView()
    {
        var vm = CreateVm(content: "   ");
        await vm.OpenPathAsync("/data/empty.csv");

        Assert.False(vm.SelectedTab!.ShowCsvTable);
    }

    // --- Ported: JSON pretty-print toggle ---

    [AvaloniaFact]
    public async Task JsonTab_InheritsThePersistedDefault_AndFormats()
    {
        var settings = Holder(new AppSettings { Editor = new EditorSettings(14, false, true, JsonPretty: true) });
        var vm = CreateVm(content: "{\"a\":1}", settings: settings);

        await vm.OpenPathAsync("/data/x.json");

        Assert.True(vm.SelectedTab!.JsonPrettyEnabled);
        Assert.Contains("\"a\": 1", vm.SelectedTab.SourceText);
        Assert.Equal("{\"a\":1}", vm.SelectedTab.DocumentText); // raw text untouched
    }

    [AvaloniaFact]
    public async Task ToggleJsonPretty_Flips_AndPersistsTheDefault()
    {
        var vm = CreateVm(content: "{\"a\":1}");
        await vm.OpenPathAsync("/data/x.json");
        Assert.Equal("{\"a\":1}", vm.SelectedTab!.SourceText); // default off

        vm.SelectedTab.ToggleJsonPrettyCommand.Execute(null);

        Assert.Contains("\"a\": 1", vm.SelectedTab.SourceText);
        Assert.True(vm.Editor.JsonPretty); // becomes the new-tab default (persisted)
    }

    [AvaloniaFact]
    public async Task JsonPretty_BrokenJson_FallsBackToRaw()
    {
        var settings = Holder(new AppSettings { Editor = new EditorSettings(14, false, true, JsonPretty: true) });
        var vm = CreateVm(content: "{broken", settings: settings);

        await vm.OpenPathAsync("/data/x.json");

        Assert.Equal("{broken", vm.SelectedTab!.SourceText);
    }

    // --- M13: HTML export ---

    [AvaloniaFact]
    public async Task ExportHtml_WritesASelfContainedFile()
    {
        var target = Path.Combine(Directory.CreateTempSubdirectory("sv-export-").FullName, "out.html");
        var dialog = new FakeFileDialogService(null) { SavePath = target };
        var vm = new MainWindowViewModel(
            dialog, new FakeFileReader("# Заголовок\n\nтекст"), new FakeThemeService(),
            new FakeRecentFilesStore(), Holder(), new FakeClipboardService(), new FakeShellService(),
            new[] { "/docs/a.md" });

        await vm.ExportHtmlCommand.ExecuteAsync(null);

        var html = File.ReadAllText(target);
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("Заголовок", html);
        Assert.Contains("Экспортировано", vm.StatusText);
        Directory.Delete(Path.GetDirectoryName(target)!, recursive: true);
    }

    [AvaloniaFact]
    public async Task ExportHtml_Cancelled_DoesNothing()
    {
        var dialog = new FakeFileDialogService(null) { SavePath = null };
        var vm = new MainWindowViewModel(
            dialog, new FakeFileReader("# T"), new FakeThemeService(), new FakeRecentFilesStore(),
            Holder(), new FakeClipboardService(), new FakeShellService(), new[] { "/docs/a.md" });

        await vm.ExportHtmlCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.SaveCalls);
        Assert.False(vm.IsErrorBarOpen);
    }

    [AvaloniaFact]
    public async Task ExportHtml_NonMarkdownTab_IsANoOp()
    {
        var dialog = new FakeFileDialogService(null) { SavePath = "C:/nope.html" };
        var vm = new MainWindowViewModel(
            dialog, new FakeFileReader("var x = 1;"), new FakeThemeService(), new FakeRecentFilesStore(),
            Holder(), new FakeClipboardService(), new FakeShellService(), new[] { "/src/a.cs" });

        await vm.ExportHtmlCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.SaveCalls); // the picker never opens for code tabs
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
    public async Task RevealInExplorer_CallsShellWithTheFilePath()
    {
        var shell = new FakeShellService();
        var vm = CreateVm(content: "x", shell: shell);
        await vm.OpenPathAsync("/docs/readme.md");

        vm.RevealInExplorerCommand.Execute(vm.SelectedTab);

        Assert.Equal(new[] { "/docs/readme.md" }, shell.Revealed);
    }

    [AvaloniaFact]
    public void RevealInExplorer_NoOp_WhenTabHasNoPath()
    {
        var shell = new FakeShellService();
        var vm = CreateVm(shell: shell);
        vm.OpenSampleCommand.Execute(null); // the sample tab has no FilePath

        vm.RevealInExplorerCommand.Execute(vm.SelectedTab);

        Assert.Empty(shell.Revealed);
    }

    [AvaloniaFact]
    public void MoveTab_ReordersAndKeepsSelection()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // 0
        vm.OpenSampleCommand.Execute(null); // 1
        vm.OpenSampleCommand.Execute(null); // 2 (selected)
        var (a, b, c) = (vm.Tabs[0], vm.Tabs[1], vm.Tabs[2]);
        var selected = vm.SelectedTab!;

        vm.MoveTab(a, 2); // drag the first tab to the end

        Assert.Equal(new[] { b, c, a }, vm.Tabs);
        Assert.Same(selected, vm.SelectedTab); // selection follows the instance, not the slot
    }

    [AvaloniaFact]
    public void MoveTab_ClampsTargetIndex()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null); // 0
        vm.OpenSampleCommand.Execute(null); // 1
        var (a, b) = (vm.Tabs[0], vm.Tabs[1]);

        vm.MoveTab(a, 99); // out of range → clamps to the last slot

        Assert.Equal(new[] { b, a }, vm.Tabs);
    }

    [AvaloniaFact]
    public void MoveTab_NoOp_ForUnknownTab()
    {
        var vm = CreateVm();
        vm.OpenSampleCommand.Execute(null);
        var orphan = DocumentTabViewModel.CreateSample(); // never added to Tabs

        vm.MoveTab(orphan, 0); // must not throw or disturb the collection

        Assert.Single(vm.Tabs);
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

    // ---- copy-as-rich-text (ported, M13) ----

    [AvaloniaFact]
    public void CopyAsRichText_PutsHtmlAndMarkdownFallbackOnTheClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateVm(args: new[] { "/docs/doc.md" }, content: "# Заголовок\n\n**жирный**", clipboard: clipboard);

        vm.CopyAsRichTextCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("<h1", clipboard.LastHtml);
        Assert.Contains("<strong>жирный</strong>", clipboard.LastHtml);
        Assert.Equal("# Заголовок\n\n**жирный**", clipboard.LastHtmlPlainFallback);
        Assert.Equal("Скопировано как форматированный текст", vm.StatusText);
    }

    [AvaloniaFact]
    public void CopyAsRichText_NonMarkdownTab_IsANoOp()
    {
        var clipboard = new FakeClipboardService();
        var vm = CreateVm(args: new[] { "/src/a.cs" }, content: "var x = 1;", clipboard: clipboard);

        vm.CopyAsRichTextCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(clipboard.LastHtml);
    }

    [AvaloniaFact]
    public void PrintViaBrowser_WritesALightHtmlAndOpensIt()
    {
        var shell = new FakeShellService();
        var vm = CreateVm(args: new[] { "/docs/doc.md" }, content: "# Печать", shell: shell);

        vm.PrintViaBrowserCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var opened = Assert.Single(shell.Opened);
        Assert.EndsWith(".print.html", opened);
        var html = File.ReadAllText(opened);
        Assert.Contains("<h1", html);
        Assert.DoesNotContain("#15151A", html); // the LIGHT stylesheet — no dark surface tones
        Assert.Equal("Открыто в браузере — печать: Ctrl+P", vm.StatusText);
        File.Delete(opened);
    }

    // ---- per-file visited / bookmarks (ported md-visited-* + bookmarks) ----

    [AvaloniaFact]
    public void ActiveHeading_MarksVisited_InTheViewState()
    {
        var store = new ViewStateStore(new FakeSettingsStore());
        var vm = CreateVm(args: new[] { "/docs/doc.md" }, content: "# A\n\ntext\n\n# B", viewState: store);
        var tab = vm.SelectedTab!;
        Assert.False(tab.IsHeadingVisited(1));

        tab.ActiveHeadingOrdinal = 1;

        Assert.True(tab.IsHeadingVisited(1));
        Assert.True(store.IsVisited("/docs/doc.md", 1));
        Assert.False(tab.IsHeadingVisited(0));
    }

    [AvaloniaFact]
    public void ToggleBookmark_Flips_AndSurfacesInThePalette()
    {
        var store = new ViewStateStore(new FakeSettingsStore());
        var vm = CreateVm(args: new[] { "/docs/doc.md" }, content: "# A\n\ntext\n\n# B", viewState: store);
        var tab = vm.SelectedTab!;
        var versionBefore = tab.ViewStateVersion;

        tab.ToggleBookmarkCommand.Execute(tab.Outline[1]);

        Assert.True(tab.IsHeadingBookmarked(1));
        Assert.True(tab.ViewStateVersion > versionBefore);
        Assert.Contains(vm.BuildPaletteItems(), i => i.Title == "Закладка: B");

        tab.ToggleBookmarkCommand.Execute(tab.Outline[1]);
        Assert.False(tab.IsHeadingBookmarked(1));
        Assert.DoesNotContain(vm.BuildPaletteItems(), i => i.Title.StartsWith("Закладка:"));
    }

    [AvaloniaFact]
    public void SampleTab_WithoutAFile_HasNoUnreadMarks()
    {
        var store = new ViewStateStore(new FakeSettingsStore());
        var vm = CreateVm(viewState: store);
        vm.OpenSampleCommand.Execute(null);
        var tab = vm.SelectedTab!;

        Assert.Null(tab.FilePath);
        Assert.True(tab.IsHeadingVisited(0));      // "everything visited" → no dots
        Assert.False(tab.IsHeadingBookmarked(0));
    }
}
