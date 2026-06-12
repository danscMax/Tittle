using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Export;
using SeriousView.Core.Services;
using SeriousView.Core.Support;
using SeriousView.Core.Text;
using SeriousView.Core.Settings;
using SeriousView.Features.Palette;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;
    private readonly IThemeService _theme;
    private readonly IRecentFilesStore _recent;
    private readonly IAppSettingsService _settings;
    private readonly IClipboardService _clipboard;
    private readonly IShellService _shell;
    private readonly DocumentExportService _export; // HTML export / print / rich-text copy collaborator
    private readonly DispatcherTimer _editorSaveTimer; // coalesces editor-option writes (zoom bursts)
    private bool _editorDirty;

    // External-change watching (M14). The shadow list makes the diff work even for a Reset
    // (CloseAllTabs clears the collection). Null watcher (tests without one) = no live-reload.
    private readonly IDocumentWatcher? _watcher;
    private readonly ViewStateStore? _viewState;
    private readonly List<string> _watchedPaths = new();

    /// <summary>Mirror the watcher's path set onto the current file-backed tabs (the
    /// CollectionChanged funnel covers add/close/close-all/replace in one place).</summary>
    private void SyncWatchedPaths()
    {
        if (_watcher is null)
            return;

        var current = Tabs.Where(t => t.FilePath is not null).Select(t => t.FilePath!).ToList();
        foreach (var gone in _watchedPaths.Except(current, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _watcher.Unwatch(gone);
            _watchedPaths.Remove(gone);
        }

        foreach (var added in current.Except(_watchedPaths, StringComparer.OrdinalIgnoreCase).ToList())
        {
            _watcher.Watch(added);
            _watchedPaths.Add(added);
        }
    }

    /// <summary>A (debounced) external change arrived for <paramref name="path"/> — mark the tab.
    /// The ACTIVE tab auto-reloads (the reader is looking at stale text right now); inactive
    /// tabs keep the dot until the user reloads them explicitly (their choice — M14 decision).
    /// A removed/renamed file keeps its tab and content; the error bar says why.</summary>
    private void HandleDocumentChanged(string path, DocumentChangeKind kind)
    {
        var tab = Tabs.FirstOrDefault(t => FilePathEquality.SameFile(t.FilePath, path));
        if (tab is null)
            return;

        tab.IsChangedOnDisk = true;
        if (kind == DocumentChangeKind.Removed)
            ShowError($"Файл удалён или переименован: {Path.GetFileName(path)}");
        else if (ReferenceEquals(tab, SelectedTab))
            PendingReload = ReloadTabAsync(tab);
    }

    /// <summary>The pending reload, for tests to await (same seam as <see cref="ErrorBarDismissal"/>).</summary>
    internal Task? PendingReload { get; private set; }

    /// <summary>Pause before the second attempt when the first load hits a transient
    /// IOException (the editor may still hold the file); tests zero it.</summary>
    internal TimeSpan ReloadRetryDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    private readonly HashSet<string> _reloadInFlight = new(StringComparer.OrdinalIgnoreCase);

    // Above this size, FromLoad's heavy immutable derivation (preprocessor + outline, several
    // full-document passes) is run on a worker so it never blocks the UI thread. Smaller documents
    // (the common case and every unit fixture) stay synchronous — the compute is instant and the
    // "open → tab present" contract holds without pumping the dispatcher.
    private const int WarmupOffloadThreshold = 200_000;

    private static Task<DocumentTabViewModel> BuildTabAsync(FileLoadResult result, string path)
        => result.Text.Length > WarmupOffloadThreshold
            ? Task.Run(() => DocumentTabViewModel.FromLoad(result, path))
            : Task.FromResult(DocumentTabViewModel.FromLoad(result, path));

    /// <summary>Export the active markdown tab as one self-contained HTML file (M13). The
    /// theme follows the app (Auto reads as dark — our default); wiki links resolve against
    /// the document's folder, exactly like the preview.</summary>
    [RelayCommand]
    private async Task ExportHtmlAsync()
    {
        if (SelectedTab is not { IsMarkdown: true } tab)
            return;

        var suggested = Path.GetFileNameWithoutExtension(tab.Header) + ".html";
        var target = await _fileDialog.SaveFileAsync(suggested);
        if (target is null)
            return;

        try
        {
            StatusText = await _export.ExportHtmlAsync(tab, target);
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, target));
        }
    }

    /// <summary>Checkbox click-to-toggle (M15): flips the N-th task box in the RAW file and
    /// reloads the tab (the M14 swap keeps the reading position). Guarded: no file on disk or
    /// unsaved editor changes → a status hint instead of clobbering anything.</summary>
    public async Task ToggleTaskAsync(DocumentTabViewModel tab, int taskIndex)
    {
        if (tab.FilePath is null)
        {
            StatusText = "Файл не сохранён на диске — чекбокс не переключить";
            return;
        }

        if (tab.IsEdited)
        {
            StatusText = "Сначала сохраните правки (Ctrl+S)";
            return;
        }

        var updated = TaskListToggle.ToggleAt(tab.DocumentText, taskIndex);
        if (updated is null)
            return;

        try
        {
            await AtomicFile.WriteAllTextAsync(tab.FilePath, updated);
            await ReloadTabAsync(tab); // immediate; the watcher's debounced reload is a no-op repeat
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, tab.FilePath));
        }
    }

    /// <summary>Save (M15, Ctrl+S): writes the live editor buffer back to the file (UTF-8 —
    /// same policy as the original's File API write). A file-backed tab then reloads itself
    /// through the M14 watcher (fresh VM, caches and reading position handled); a tab without
    /// a file (the sample) asks for a target and opens it. No-ops without an attached editor.</summary>
    [RelayCommand]
    private async Task SaveActiveTabAsync()
    {
        if (SelectedTab is not { } tab || tab.EditorTextProvider is not { } pull)
            return;

        // Never silently clobber a file that changed on disk since we loaded it. M14 marks the dot,
        // but a reload that failed (file briefly locked) or never ran leaves a stale buffer — saving
        // it would overwrite the newer on-disk content. Require an explicit reload or Save-As.
        if (tab.FilePath is not null && tab.IsChangedOnDisk)
        {
            ShowError("Файл изменён на диске — перезагрузите вкладку (контекстное меню/палитра) " +
                "или сохраните как новый файл.");
            return;
        }

        // The editor shows SourceText — possibly a DISPLAY transform (pretty JSON, smart
        // typography). Editing is blocked while a transform is active (the editor is read-only),
        // so an edited buffer is always the raw text; an unedited save writes the raw truth.
        var text = tab.IsEdited ? pull() : tab.DocumentText;
        var target = tab.FilePath;
        if (target is null)
        {
            target = await _fileDialog.SaveFileAsync(Path.ChangeExtension(tab.Header, ".md"));
            if (target is null)
                return;
        }

        try
        {
            await AtomicFile.WriteAllTextAsync(target, text);
            tab.IsEdited = false;
            StatusText = $"Сохранено: {Path.GetFileName(target)}";
            if (tab.FilePath is null)
                await OpenPathAsync(target); // the sample saved to disk → open the real file tab
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, target));
        }
    }

    /// <summary>Print / save-as-PDF (ported, M13): the LIGHT-theme HTML export goes to a temp
    /// file and opens in the default browser — its print dialog (Ctrl+P) covers both paper and
    /// selectable-text PDF. A native rasterized PDF was deliberately not built: rendering the
    /// preview off-screen trips the embedded-editor geometry, and the browser output is better.</summary>
    [RelayCommand]
    private async Task PrintViaBrowserAsync()
    {
        if (SelectedTab is not { IsMarkdown: true } tab)
            return;

        try
        {
            StatusText = await _export.PrintViaBrowserAsync(tab);
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, tab.Header));
        }
    }

    /// <summary>Copy-as-rich-text (ported, M13): the themed HTML export goes onto the
    /// clipboard as HTML (CF_HTML on Windows) with the raw markdown as the plain fallback.</summary>
    [RelayCommand]
    private async Task CopyAsRichTextAsync()
    {
        if (SelectedTab is not { IsMarkdown: true } tab)
            return;

        try
        {
            StatusText = await _export.CopyAsRichTextAsync(tab);
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, tab.Header));
        }
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        var target = await _fileDialog.SaveFileAsync("seriousview-settings.json");
        if (target is null)
            return;

        try
        {
            await AtomicFile.WriteAllTextAsync(target, SettingsTransfer.Serialize(_settings.Current));
            StatusText = $"Настройки сохранены: {Path.GetFileName(target)}";
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, target));
        }
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        var paths = await _fileDialog.PickFilesAsync();
        if (paths.Count == 0)
            return;

        try
        {
            // S7: settings are a few KB; reject anything pathological before reading it all into
            // memory (a multi-GB / deeply-nested JSON could OOM or spin). The typed-record whitelist
            // in SettingsTransfer.Parse already blocks gadget payloads — this guards size only.
            if (new FileInfo(paths[0]).Length > MaxImportBytes)
            {
                ShowError($"Файл слишком большой для настроек: {Path.GetFileName(paths[0])}");
                return;
            }

            var raw = await File.ReadAllTextAsync(paths[0]);
            var (status, parsed) = SettingsTransfer.Parse(raw);
            if (status == SettingsTransfer.ParseStatus.NotSettings)
            {
                ShowError($"Файл не содержит настроек: {Path.GetFileName(paths[0])}");
                return;
            }

            if (status != SettingsTransfer.ParseStatus.Ok || parsed is null)
            {
                ShowError($"Не удалось открыть файл: {Path.GetFileName(paths[0])}");
                return;
            }

            // Merge the PREFERENCE fields only — an imported file must never replace this
            // machine's session (open files) or window placement.
            _settings.Update(_settings.Current with
            {
                Theme = parsed.Theme,
                Editor = parsed.Editor,
                Layout = parsed.Layout,
            });
            _theme.SetMode(parsed.Theme);
            ApplyImportedOptions(parsed);
            StatusText = "Настройки импортированы";
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, paths[0]));
        }
    }

    /// <summary>Live-apply the imported editor/layout options to the shared observable
    /// instances (window/session parts take effect on the next launch).</summary>
    private void ApplyImportedOptions(AppSettings parsed)
    {
        var editor = EditorOptions.FromSettings(parsed.Editor);
        Editor.FontSize = editor.FontSize;
        Editor.WordWrap = editor.WordWrap;
        Editor.ShowLineNumbers = editor.ShowLineNumbers;
        Editor.JsonPretty = editor.JsonPretty;
        Editor.CsvAsTable = editor.CsvAsTable;
        Editor.SmartTypography = editor.SmartTypography;

        var layout = LayoutOptions.FromSettings(parsed.Layout);
        Layout.MenuPlacement = layout.MenuPlacement;
        Layout.ToolbarMode = layout.ToolbarMode;
        Layout.ViewTogglePlacement = layout.ViewTogglePlacement;
        Layout.ShowOmnibar = layout.ShowOmnibar;
        Layout.ShowRail = layout.ShowRail;
        Layout.ReadingMode = layout.ReadingMode;
        Layout.OutlineWidth = layout.OutlineWidth;
    }

    /// <summary>Reload a tab from disk (tab context menu / the dirty dot / the palette).</summary>
    [RelayCommand]
    private Task ReloadTab(DocumentTabViewModel? tab)
    {
        if (tab?.FilePath is null)
            return Task.CompletedTask;
        return PendingReload = ReloadTabAsync(tab);
    }

    /// <summary>Reload = build a FRESH tab VM and swap it in place: DocumentText is immutable by
    /// design, so replacing the tab refreshes every cache (preview, outline, search, wiki-link
    /// existence snapshot) through the same FromLoad path every open uses.</summary>
    private async Task ReloadTabAsync(DocumentTabViewModel tab)
    {
        if (tab.FilePath is not { } path || !_reloadInFlight.Add(path))
            return;

        try
        {
            FileLoadResult result;
            try
            {
                result = await _fileReader.LoadAsync(path);
            }
            catch (IOException)
            {
                await Task.Delay(ReloadRetryDelay);
                result = await _fileReader.LoadAsync(path);
            }

            // Build the fresh tab off the UI thread for large files (immutable data → thread-safe);
            // ReplaceTab marshals back here. Small files stay synchronous (see BuildTabAsync).
            var fresh = await BuildTabAsync(result, path);
            fresh.ViewMode = tab.ViewMode;          // the reader's preview/source choice survives
            fresh.RestoreAnchor = tab.ReadingAnchor; // ...and so does the reading position (C3)
            ReplaceTab(tab, fresh);
            // After the swap: selecting the fresh tab blanks StatusText, so write it last.
            StatusText = $"Файл обновлён: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            // Keep the old tab and its content readable; the dot stays — disk still differs.
            ShowError(DescribeError(ex, path));
        }
        finally
        {
            _reloadInFlight.Remove(path);
        }
    }

    /// <summary>Indexed in-place swap. Restores the selection explicitly — the bound ListBox
    /// nulls SelectedItem on collection changes (the MoveTab lesson).</summary>
    internal void ReplaceTab(DocumentTabViewModel oldTab, DocumentTabViewModel newTab)
    {
        var index = Tabs.IndexOf(oldTab);
        if (index < 0)
            return;

        AdoptTab(newTab);
        var wasSelected = ReferenceEquals(SelectedTab, oldTab);
        Tabs[index] = newTab;
        if (wasSelected || SelectedTab is null)
            SelectedTab = newTab;
        oldTab.Dispose(); // release the swapped-out tab's debounce timer
    }

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    public bool HasRecent => _recent.Items.Count > 0;

    /// <summary>Recent files projected for display (name + folder) with a self-contained open command —
    /// bound by the ☰ File ▸ Recent submenu and the welcome list. Rebuilt when the recent list changes.</summary>
    [ObservableProperty]
    private IReadOnlyList<RecentFileItem> _recentItems = Array.Empty<RecentFileItem>();

    /// <summary>True when at least one document tab is open (drives the empty placeholder).</summary>
    public bool HasTabs => Tabs.Count > 0;

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _title = "SeriousView";

    /// <summary>Editable text of the omnibar address field. Seeded from the active tab's path and
    /// rewritten on every tab change; only acted upon when the user presses Enter (open) — so it's a
    /// scratch buffer, never a mutation of the tab itself. Esc reverts it via <see cref="ResetOmnibar"/>.</summary>
    [ObservableProperty]
    private string _omnibarText = "";

    /// <summary>Restore the omnibar to the active tab's path (Esc / cancel an in-progress edit).</summary>
    public void ResetOmnibar() => OmnibarText = SelectedTab?.FilePath ?? "";

    /// <summary>Idle hint shown in the status bar on the welcome screen (no tab open): a short
    /// call-to-action instead of a bare "Готово".</summary>
    private const string WelcomeHint = "Откройте файл (Ctrl+O), перетащите его сюда или выберите из недавних";

    /// <summary>Status bar left-segment text: the welcome hint when idle, cleared while a tab is
    /// active (the tab's own metrics show on the right), or a read-error message.</summary>
    [ObservableProperty]
    private string _statusText = WelcomeHint;

    /// <summary>Message in the error InfoBar (#28) — a load failure surfaced prominently (the
    /// status-bar text alone is easy to miss). Auto-dismissed; the bar's own ✕ closes it too.</summary>
    [ObservableProperty]
    private string? _errorBarMessage;

    /// <summary>Whether the error InfoBar is shown. Two-way: the InfoBar's close button writes false.</summary>
    [ObservableProperty]
    private bool _isErrorBarOpen;

    private CancellationTokenSource? _errorBarCts;

    /// <summary>Auto-dismiss delay for the error InfoBar; tests shorten it.</summary>
    internal TimeSpan ErrorBarAutoDismissDelay { get; set; } = TimeSpan.FromSeconds(7);

    /// <summary>Max size of an importable settings file (S7); tests lower it. ~1 MB ≫ any real
    /// settings.json (a few KB), small enough to refuse a pathological/oversized file before reading.</summary>
    internal long MaxImportBytes { get; set; } = 1024 * 1024;

    /// <summary>The pending auto-dismiss, for tests to await. A superseded timer completes
    /// without touching the bar (its token is cancelled by the newer error).</summary>
    internal Task? ErrorBarDismissal { get; private set; }

    /// <summary>Show <paramref name="message"/> in the error InfoBar and (re)start its auto-dismiss.</summary>
    private void ShowError(string message)
    {
        _errorBarCts?.Cancel();
        _errorBarCts?.Dispose();
        var cts = _errorBarCts = new CancellationTokenSource();
        ErrorBarMessage = message;
        IsErrorBarOpen = true;
        ErrorBarDismissal = AutoDismissErrorBarAsync(cts.Token);
    }

    private async Task AutoDismissErrorBarAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ErrorBarAutoDismissDelay, token);
        }
        catch (TaskCanceledException)
        {
            return; // superseded by a newer error — its own timer owns the bar now
        }

        IsErrorBarOpen = false;
    }

    /// <summary>Whether the user has the outline pane turned on (per-window, persists across tabs).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlinePaneVisible))]
    private bool _isOutlineVisible = true;

    /// <summary>The outline pane is shown only when enabled AND the active tab has headings.</summary>
    public bool IsOutlinePaneVisible => IsOutlineVisible && (SelectedTab?.HasOutline ?? false);

    [RelayCommand]
    private void ToggleOutline() => IsOutlineVisible = !IsOutlineVisible;

    /// <summary>Toggle the focused reading column + side decor in the markdown preview.</summary>
    [RelayCommand]
    private void ToggleReadingMode() => Layout.ReadingMode = !Layout.ReadingMode;

    /// <summary>Editor display options shared by every tab's source editor (font zoom, wrap,
    /// line numbers). Bound by <c>DocumentView</c>; persisted whenever it changes.</summary>
    public EditorOptions Editor { get; }

    /// <summary>Shell layout options (menu / toolbar / view-toggle placement, omnibar / rail). Drives
    /// the chrome (M7.5); persisted whenever it changes. Default is the etalon (menu hidden behind ☰).</summary>
    public LayoutOptions Layout { get; }

    [RelayCommand]
    private void ZoomIn() => Editor.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => Editor.ZoomOut();

    [RelayCommand]
    private void ZoomReset() => Editor.ResetZoom();

    [RelayCommand]
    private void ToggleWordWrap() => Editor.ToggleWordWrap();

    [RelayCommand]
    private void ToggleLineNumbers() => Editor.ToggleLineNumbers();

    /// <summary>Open the go-to-line overlay on the active tab (Ctrl+G), only in source view.</summary>
    [RelayCommand]
    private void OpenGoToLine()
    {
        if (SelectedTab?.ShowSource == true)
            SelectedTab.IsGoToLineOpen = true;
    }

    /// <summary>Open the find bar on the active tab (Ctrl+F). The tab switches a markdown preview to
    /// source so matches are visible; a binary/too-large/empty (notice) tab ignores it.</summary>
    [RelayCommand]
    private void OpenSearch() => SelectedTab?.OpenSearchCommand.Execute(null);

    /// <summary>Raised when the user opens layout settings (☰ ▸ Раскладка or the palette); the window is
    /// a view concern, so the shell's code-behind shows it.</summary>
    public event Action? LayoutSettingsRequested;

    /// <summary>Raised with the computed stats when the user asks for document statistics
    /// (ported stats panel); the window is shown by the shell's code-behind.</summary>
    public event Action<TextStats>? StatsRequested;

    /// <summary>Raised when the user opens the shortcuts help (F1 / menu / palette).</summary>
    public event Action? HelpRequested;

    [RelayCommand]
    private void ShowHelp() => HelpRequested?.Invoke();

    /// <summary>Raised when the user opens "Поддержать автора" (♥ button / menu / palette); the donation
    /// window is a view concern, so the shell's code-behind shows it.</summary>
    public event Action? DonateRequested;

    [RelayCommand]
    private void ShowDonate() => DonateRequested?.Invoke();

    /// <summary>Show document statistics for the active tab (palette / menu).</summary>
    [RelayCommand]
    private void ShowStats()
    {
        if (SelectedTab is { } tab)
            StatsRequested?.Invoke(TextStatistics.Compute(tab.DocumentText));
    }

    /// <summary>Open the Settings ▸ Layout window — it binds to the shared <see cref="Layout"/>, so its
    /// toggles persist and re-render the chrome live.</summary>
    [RelayCommand]
    private void OpenLayoutSettings() => LayoutSettingsRequested?.Invoke();

    public MainWindowViewModel(
        IFileDialogService fileDialog, IFileReader fileReader, IThemeService theme,
        IRecentFilesStore recent, IAppSettingsService settings, IClipboardService clipboard,
        IShellService shell, string[] args, IDocumentWatcher? documentWatcher = null,
        ViewStateStore? viewState = null)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;
        _theme = theme;
        _recent = recent;
        _settings = settings;
        _clipboard = clipboard;
        _shell = shell;
        _export = new DocumentExportService(theme, shell, clipboard);
        _watcher = documentWatcher;
        _viewState = viewState;
        if (_watcher is not null)
            _watcher.Changed += OnWatcherChanged;

        // Shared editor options, restored from settings. Persisted on change, but DEBOUNCED: a Ctrl+wheel
        // zoom spins ZoomIn/ZoomOut per notch, and each immediate _settings.Update did a synchronous
        // JSON-serialize + temp-file + File.Replace on the UI thread. Coalesce a burst into one write
        // (the in-memory Editor is still updated instantly); FlushEditorSettings() lands it on close.
        Editor = EditorOptions.FromSettings(_settings.Current.Editor);
        _editorSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _editorSaveTimer.Tick += OnEditorSaveTimerTick;
        // Editor/Layout are shared singletons; their closures would root this VM for the whole
        // app/window lifetime, so the handlers are named and detached in Dispose().
        Editor.PropertyChanged += OnEditorPropertyChanged;

        // Shared shell-layout options, same restore-and-persist pattern. Drives the chrome in later phases.
        Layout = LayoutOptions.FromSettings(_settings.Current.Layout);
        Layout.PropertyChanged += OnLayoutPropertyChanged;

        Tabs.CollectionChanged += OnTabsCollectionChanged;
        _theme.Changed += OnThemeChanged;
        _recent.Changed += OnRecentChanged;
        RefreshRecentItems(); // seed from any persisted recent files

        // Startup precedence: an explicit file argument wins, then the last session, else welcome.
        // All paths are async and guarded so a missing/locked/unreadable file can't crash startup.
        if (args.Length > 0)
            _ = OpenPathAsync(args[0]);
        else if (_settings.Current.Session is { OpenFiles.Count: > 0 } session)
            _ = RestoreSessionAsync(session);
        // Otherwise no tab is opened — the welcome view is shown while HasTabs is false.
    }

    /// <summary>Persist any pending (debounced) editor-option change immediately. Called by the coalescing
    /// timer and by the window on close, so a zoom/wrap/line-number change is never lost.</summary>
    public void FlushEditorSettings()
    {
        _editorSaveTimer.Stop();
        if (!_editorDirty)
            return;
        _editorDirty = false;
        _settings.Update(_settings.Current with { Editor = Editor.ToSettings() });
    }

    // Named subscription handlers — kept as methods (not lambdas) so Dispose() can detach them and
    // free this VM, which would otherwise be rooted by the shared Editor/Layout/watcher singletons.
    private void OnWatcherChanged(string path, DocumentChangeKind kind) =>
        Dispatcher.UIThread.Post(() => HandleDocumentChanged(path, kind));

    private void OnEditorSaveTimerTick(object? sender, EventArgs e) => FlushEditorSettings();

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _editorDirty = true;
        _editorSaveTimer.Stop();
        _editorSaveTimer.Start();
    }

    private void OnLayoutPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        _settings.Update(_settings.Current with { Layout = Layout.ToSettings() });

    private void OnTabsCollectionChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasTabs));
        SyncWatchedPaths();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(CurrentTheme));

    private void OnRecentChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasRecent));
        RefreshRecentItems();
    }

    /// <summary>Detach every subscription this VM made and stop the editor-save timer (flushing any
    /// pending change first so no setting is lost). <see cref="Editor"/>/<see cref="Layout"/> and the
    /// injected <c>_watcher</c> are SHARED singletons — we only detach our handlers from them, never
    /// dispose them (the watcher is a DI singleton disposed at app shutdown in App.axaml.cs). Called by
    /// the window on close so a recreated/multi-window VM doesn't leak via those long-lived closures.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Land any debounced editor-option change, then stop the timer.
        FlushEditorSettings();
        _editorSaveTimer.Tick -= OnEditorSaveTimerTick;
        _editorSaveTimer.Stop();

        Editor.PropertyChanged -= OnEditorPropertyChanged;
        Layout.PropertyChanged -= OnLayoutPropertyChanged;
        Tabs.CollectionChanged -= OnTabsCollectionChanged;
        _theme.Changed -= OnThemeChanged;
        _recent.Changed -= OnRecentChanged;
        if (_watcher is not null)
            _watcher.Changed -= OnWatcherChanged;
        // NOTE: _watcher is a DI singleton (App.axaml.cs) shared across windows and disposed at app
        // shutdown — detach only, do NOT dispose it here.

        // Cancel any in-flight error-bar auto-dismiss so a window closed with an error showing
        // doesn't leave a 7 s Task.Delay rooted that then pokes IsErrorBarOpen on this disposed VM.
        _errorBarCts?.Cancel();
        _errorBarCts?.Dispose();
        _errorBarCts = null;

        // Release each open tab's debounce timer so a closing window with live tabs leaves nothing
        // rooted on the dispatcher timer queue.
        foreach (var tab in Tabs)
            tab.Dispose();
    }

    /// <summary>Build the Ctrl+K command-palette entries from the shell's own commands (+ the active
    /// markdown tab's view toggle and the recent files). Rebuilt per open so it reflects current state.</summary>
    public IReadOnlyList<PaletteItem> BuildPaletteItems()
    {
        var items = new List<PaletteItem>
        {
            new("Открыть файл…", OpenFileCommand, "Ctrl+O"),
            new("Открыть пример", OpenSampleCommand),
            new("Закрыть вкладку", CloseActiveTabCommand, "Ctrl+W"),
            new("Следующая вкладка", SelectNextTabCommand, "Ctrl+Tab"),
            new("Предыдущая вкладка", SelectPreviousTabCommand, "Ctrl+Shift+Tab"),
            new("Оглавление", ToggleOutlineCommand),
            new("Декоративный фон", ToggleReadingModeCommand),
            new("Статистика документа", ShowStatsCommand),
            new("Найти…", OpenSearchCommand, "Ctrl+F"),
            new("Перейти к строке…", OpenGoToLineCommand, "Ctrl+G"),
            new("Перенос строк", ToggleWordWrapCommand, "Alt+Z"),
            new("Номера строк", ToggleLineNumbersCommand, "Ctrl+L"),
            new("Масштаб: больше", ZoomInCommand, "Ctrl++"),
            new("Масштаб: меньше", ZoomOutCommand, "Ctrl+−"),
            new("Масштаб: сбросить", ZoomResetCommand, "Ctrl+0"),
            new("Тема: тёмная", SetThemeCommand, parameter: ThemeMode.Dark),
            new("Тема: полночь", SetThemeCommand, parameter: ThemeMode.Midnight),
            new("Тема: океан", SetThemeCommand, parameter: ThemeMode.Ocean),
            new("Тема: светлая", SetThemeCommand, parameter: ThemeMode.Light),
            new("Тема: авто", SetThemeCommand, parameter: ThemeMode.Auto),
            new("Настройки: раскладка…", OpenLayoutSettingsCommand),
            new("Настройки: экспорт…", ExportSettingsCommand),
            new("Настройки: импорт…", ImportSettingsCommand),
            new("Справка: горячие клавиши", ShowHelpCommand, "F1"),
            new("Поддержать автора…", ShowDonateCommand),
        };

        if (SelectedTab is { IsMarkdown: true } tab)
        {
            items.Add(new PaletteItem("Переключить предпросмотр / исходник", tab.ToggleViewModeCommand));
            items.Add(new PaletteItem("Экспорт в HTML…", ExportHtmlCommand));
            items.Add(new PaletteItem("Копировать как форматированный текст", CopyAsRichTextCommand));
            items.Add(new PaletteItem("Печать / PDF (через браузер)…", PrintViaBrowserCommand));
        }

        if (SelectedTab is not null)
            items.Add(new PaletteItem("Сохранить", SaveActiveTabCommand));

        if (SelectedTab is { FilePath: not null } fileTab)
            items.Add(new PaletteItem("Перезагрузить с диска", ReloadTabCommand, parameter: fileTab));

        if (SelectedTab is { IsJson: true } jsonTab)
            items.Add(new PaletteItem("Форматировать JSON (вкл/выкл)", jsonTab.ToggleJsonPrettyCommand));

        if (SelectedTab is { Delimiter: not null } csvTab)
            items.Add(new PaletteItem("Таблица / исходник", csvTab.ToggleCsvViewCommand));

        if (SelectedTab is { IsPlainText: true } textTab)
        {
            items.Add(new PaletteItem("Умная типографика (вкл/выкл)", textTab.ToggleSmartTypographyCommand));
            if (textTab.HasOutline)
            {
                items.Add(new PaletteItem("Свернуть все секции", textTab.FoldAllSectionsCommand));
                items.Add(new PaletteItem("Развернуть все секции", textTab.UnfoldAllSectionsCommand));
            }
        }

        // Bookmarked headings of the active document jump via the same navigation seam (ported).
        if (_viewState is not null && SelectedTab is { FilePath: { } statePath } bmTab)
        {
            foreach (var ordinal in _viewState.BookmarksFor(statePath))
            {
                if (ordinal >= 0 && ordinal < bmTab.Outline.Count)
                    items.Add(new PaletteItem($"Закладка: {bmTab.Outline[ordinal].Text}",
                        bmTab.NavigateToHeadingCommand, parameter: bmTab.Outline[ordinal]));
            }
        }

        foreach (var r in RecentItems)
            items.Add(new PaletteItem($"Недавнее: {r.Name}", r.OpenCommand));

        return items;
    }

    /// <summary>Reopens the documents from the saved session, skipping any that are gone/unreadable
    /// (restore is best-effort), then selects the saved tab. The skipped files are reported in one
    /// summary error bar (#28) — tabs must not vanish silently between launches.</summary>
    private async Task RestoreSessionAsync(SessionState session)
    {
        var missing = new List<string>();
        foreach (var path in session.OpenFiles)
        {
            try
            {
                var result = await _fileReader.LoadAsync(path);
                AddTab(await BuildTabAsync(result, path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A genuinely missing/unreadable file from the last session is expected; an
                // unexpected type (e.g. an NRE in FromLoad) must surface to the crash logger
                // instead of masquerading as "не удалось открыть".
                missing.Add(Path.GetFileName(path));
            }
        }

        if (Tabs.Count > 0)
            SelectedTab = Tabs[Math.Clamp(session.ActiveIndex, 0, Tabs.Count - 1)];

        if (missing.Count > 0)
            ShowError(missing.Count == 1
                ? $"Не удалось открыть файл из прошлой сессии: {missing[0]}"
                : $"Не удалось открыть файлы из прошлой сессии: {string.Join(", ", missing)}");
    }

    /// <summary>Persist the per-file visited/bookmark state (called where the session is saved;
    /// bookmark toggles flush eagerly, visited marks accumulate until here).</summary>
    public void FlushViewState() => _viewState?.Flush();

    public SessionState GetSession()
    {
        var withPath = Tabs.Where(t => t.FilePath is not null).ToList();
        var paths = withPath.Select(t => t.FilePath!).ToList();
        var active = SelectedTab is not null ? withPath.IndexOf(SelectedTab) : -1;
        return new SessionState(paths, active < 0 ? 0 : active);
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        foreach (var path in await _fileDialog.PickFilesAsync())
            await OpenPathAsync(path);
    }

    [RelayCommand]
    private Task OpenRecent(string path) => OpenPathAsync(path);

    /// <summary>Rebuild <see cref="RecentItems"/> from the store (each item carries its own open command).</summary>
    private void RefreshRecentItems() =>
        RecentItems = _recent.Items.Select(p => new RecentFileItem(p, () => _ = OpenPathAsync(p))).ToList();

    /// <summary>Opens the built-in sample document (offered on the welcome screen).</summary>
    [RelayCommand]
    private void OpenSample() => AddTab(DocumentTabViewModel.CreateSample());

    /// <summary>Loads <paramref name="path"/> into a new active tab and records it as recent. If the file
    /// is already open, its existing tab is activated instead of loading a duplicate. Real I/O failures
    /// become a friendly status message instead of a crash.</summary>
    public async Task OpenPathAsync(string path)
    {
        try
        {
            // Reopening an already-open file just activates its tab — no duplicate. Every open path
            // funnels through here (Ctrl+O, recent, drag-drop, single-instance forwarding), so this
            // one check covers them all. RestoreSessionAsync deliberately bypasses this (empty list).
            var existing = Tabs.FirstOrDefault(t => FilePathEquality.SameFile(t.FilePath, path));
            if (existing is not null)
            {
                SelectedTab = existing;
                _recent.Add(path);
                return;
            }

            var result = await _fileReader.LoadAsync(path);
            AddTab(await BuildTabAsync(result, path));
            _recent.Add(path);
        }
        catch (Exception ex)
        {
            var message = DescribeError(ex, path);
            // With a tab open the left status segment is otherwise empty, so it can echo the error for
            // context until the next tab change. On the WELCOME screen (no tabs) there is no next tab
            // change to clear it, so the error would stick forever over the welcome hint — keep the
            // hint there and let the (prominent, auto-dismissing) InfoBar carry the error instead.
            StatusText = HasTabs ? message : WelcomeHint;
            ShowError(message);
        }
    }

    /// <summary>Maps a load failure to a friendly Russian message by exception type.</summary>
    private static string DescribeError(Exception ex, string path)
    {
        var name = Path.GetFileName(path);
        return ex switch
        {
            FileNotFoundException or DirectoryNotFoundException => $"Файл не найден: {name}",
            UnauthorizedAccessException => $"Нет доступа к файлу: {name}",
            IOException => $"Файл занят или ошибка чтения: {name}",
            _ => $"Не удалось открыть файл: {name}",
        };
    }

    [RelayCommand]
    private void CloseTab(DocumentTabViewModel? tab)
    {
        if (tab is null)
            return;

        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;

        Tabs.Remove(tab);
        tab.Dispose();

        if (Tabs.Count == 0)
            SelectedTab = null;
        else if (ReferenceEquals(SelectedTab, tab) || SelectedTab is null)
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
    }

    /// <summary>Close the active tab (Ctrl+W).</summary>
    [RelayCommand]
    private void CloseActiveTab()
    {
        if (SelectedTab is not null)
            CloseTab(SelectedTab);
    }

    /// <summary>Close every tab except <paramref name="tab"/> (tab context menu); it becomes active.</summary>
    [RelayCommand]
    private void CloseOtherTabs(DocumentTabViewModel? tab)
    {
        if (tab is null || !Tabs.Contains(tab))
            return;

        foreach (var other in Tabs.Where(t => !ReferenceEquals(t, tab)).ToList())
        {
            Tabs.Remove(other);
            other.Dispose();
        }

        SelectedTab = tab;
    }

    /// <summary>Close every tab to the right of <paramref name="tab"/> (tab context menu). If the active
    /// tab was among those closed, the selection falls back to <paramref name="tab"/>.</summary>
    [RelayCommand]
    private void CloseTabsToRight(DocumentTabViewModel? tab)
    {
        if (tab is null)
            return;

        var index = Tabs.IndexOf(tab);
        if (index < 0)
            return;

        for (var i = Tabs.Count - 1; i > index; i--)
        {
            var removed = Tabs[i];
            Tabs.RemoveAt(i);
            removed.Dispose();
        }

        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
            SelectedTab = tab;
    }

    /// <summary>Close every tab (tab context menu).</summary>
    [RelayCommand]
    private void CloseAllTabs()
    {
        foreach (var tab in Tabs)
            tab.Dispose();
        Tabs.Clear();
        SelectedTab = null;
    }

    /// <summary>Copy the tab's full file path to the clipboard (tab context menu). No-op for a tab
    /// without a backing file (the sample).</summary>
    [RelayCommand]
    private async Task CopyFilePath(DocumentTabViewModel? tab)
    {
        if (tab?.FilePath is { } path)
            await _clipboard.SetTextAsync(path);
    }

    /// <summary>Copy the tab's file name to the clipboard (tab context menu). No-op when unsaved.</summary>
    [RelayCommand]
    private async Task CopyFileName(DocumentTabViewModel? tab)
    {
        if (tab?.FilePath is { } path)
            await _clipboard.SetTextAsync(Path.GetFileName(path));
    }

    /// <summary>Reveal the tab's file in the OS file manager (tab context menu). No-op when unsaved.</summary>
    [RelayCommand]
    private void RevealInExplorer(DocumentTabViewModel? tab)
    {
        if (tab?.FilePath is { } path)
            _shell.RevealInExplorer(path);
    }

    /// <summary>Move <paramref name="tab"/> to <paramref name="targetIndex"/> (tab drag-reorder, driven by
    /// the view's pointer gesture). The same instance stays selected; the index is clamped to the
    /// collection. A no-op when the tab isn't open or is already at the target.</summary>
    public void MoveTab(DocumentTabViewModel tab, int targetIndex)
    {
        var from = Tabs.IndexOf(tab);
        if (from < 0)
            return;

        var to = Math.Clamp(targetIndex, 0, Tabs.Count - 1);
        if (to == from)
            return;

        // The bound ListBox drops its SelectedItem when the collection moves and writes that null back
        // through the two-way binding, blanking the active tab. Capture and restore the selection so the
        // dragged tab (and its shown content) survives the reorder. (No-op in headless VM tests, where
        // there's no ListBox to clear it — hence this is covered by the drag visual check, not a unit test.)
        var selected = SelectedTab;
        Tabs.Move(from, to);
        if (!ReferenceEquals(SelectedTab, selected))
            SelectedTab = selected;
    }

    /// <summary>Activate the next tab, wrapping around (Ctrl+Tab).</summary>
    [RelayCommand]
    private void SelectNextTab() => CycleTab(1);

    /// <summary>Activate the previous tab, wrapping around (Ctrl+Shift+Tab).</summary>
    [RelayCommand]
    private void SelectPreviousTab() => CycleTab(-1);

    private void CycleTab(int direction)
    {
        if (Tabs.Count == 0)
            return;
        if (SelectedTab is null)
        {
            SelectedTab = Tabs[0];
            return;
        }

        var n = Tabs.Count;
        SelectedTab = Tabs[(Tabs.IndexOf(SelectedTab) + direction + n) % n];
    }

    /// <summary>Current theme mode — drives the radio check-marks in the ☰ View ▸ Theme submenu.</summary>
    public ThemeMode CurrentTheme => _theme.Mode;

    [RelayCommand]
    private void ToggleTheme() => _theme.Cycle();

    /// <summary>Apply a specific theme mode (the ☰ View ▸ Theme radio items).</summary>
    [RelayCommand]
    private void SetTheme(ThemeMode mode) => _theme.SetMode(mode);

    private void AddTab(DocumentTabViewModel tab)
    {
        AdoptTab(tab);
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>Shared-state wiring every tab gets, whether added or swapped in by a reload.</summary>
    private void AdoptTab(DocumentTabViewModel tab)
    {
        tab.Editor = Editor;   // share one editor-options instance across all tabs
        tab.Layout = Layout;   // share the shell-layout options (reading mode)
        tab.Shell = this;      // back-reference for the tab's context-menu commands
        tab.ViewState = _viewState; // per-file visited/bookmark store (ported)
        tab.JsonPrettyEnabled = tab.IsJson && Editor.JsonPretty;        // persisted default (ported)
        tab.CsvAsTableEnabled = tab.Delimiter is not null && Editor.CsvAsTable; // ditto
        tab.SmartTypographyEnabled = tab.IsPlainText && Editor.SmartTypography; // ditto
    }

    // Keep exactly one tab active so the body shows only its (kept-alive) DocumentView.
    // Q8: one selection-changed hook (the 2-arg overload sees both old and new) instead of two
    // partials whose split made the IsOutlinePaneVisible notify look incidental.
    partial void OnSelectedTabChanged(DocumentTabViewModel? oldValue, DocumentTabViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsActive = false;
        if (newValue is not null)
            newValue.IsActive = true;

        Title = newValue is null ? "SeriousView" : newValue.Header + " — SeriousView";
        // Status bar is segmented: the left segment shows messages — the welcome hint when no tab is
        // open, otherwise cleared (the right segment binds the active tab's metrics directly in the
        // view). A read error overwrites this until the next tab change.
        StatusText = newValue is null ? WelcomeHint : "";
        // Reflect the active document's path in the editable omnibar address field.
        OmnibarText = newValue?.FilePath ?? "";
        // IsOutlinePaneVisible reads SelectedTab.HasOutline, but [NotifyPropertyChangedFor] on
        // _isOutlineVisible only covers that field — a tab change is the OTHER input to the property,
        // so it must be raised here explicitly (load-bearing, not incidental).
        OnPropertyChanged(nameof(IsOutlinePaneVisible));
    }
}
