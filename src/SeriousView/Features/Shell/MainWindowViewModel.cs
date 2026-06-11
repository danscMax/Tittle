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
using SeriousView.Core.Text;
using SeriousView.Core.Settings;
using SeriousView.Features.Palette;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;
    private readonly IThemeService _theme;
    private readonly IRecentFilesStore _recent;
    private readonly IAppSettingsService _settings;
    private readonly IClipboardService _clipboard;
    private readonly IShellService _shell;
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
            var dark = _theme.Mode != ThemeMode.Light;
            var html = HtmlExporter.Export(tab.DocumentText, tab.Header, dark, tab.BuildWikiResolver());
            await File.WriteAllTextAsync(target, html);
            StatusText = $"Экспортировано: {Path.GetFileName(target)}";
        }
        catch (Exception ex)
        {
            ShowError(DescribeError(ex, target));
        }
    }

    // Settings import/export (ported). Whitelisting comes by construction: the file is
    // deserialized into the typed AppSettings record — unknown keys are simply ignored.
    private static readonly System.Text.Json.JsonSerializerOptions SettingsJson =
        new() { TypeInfoResolver = AppJsonContext.Default, WriteIndented = true };

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        var target = await _fileDialog.SaveFileAsync("seriousview-settings.json");
        if (target is null)
            return;

        try
        {
            await File.WriteAllTextAsync(target,
                System.Text.Json.JsonSerializer.Serialize(_settings.Current, SettingsJson));
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
            var parsed = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                await File.ReadAllTextAsync(paths[0]), SettingsJson);
            if (parsed is null)
            {
                ShowError($"Не удалось открыть файл: {Path.GetFileName(paths[0])}");
                return;
            }

            _settings.Update(parsed);
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

            var fresh = DocumentTabViewModel.FromLoad(result, path);
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
        _watcher = documentWatcher;
        _viewState = viewState;
        if (_watcher is not null)
            _watcher.Changed += (path, kind) =>
                Dispatcher.UIThread.Post(() => HandleDocumentChanged(path, kind));

        // Shared editor options, restored from settings. Persisted on change, but DEBOUNCED: a Ctrl+wheel
        // zoom spins ZoomIn/ZoomOut per notch, and each immediate _settings.Update did a synchronous
        // JSON-serialize + temp-file + File.Replace on the UI thread. Coalesce a burst into one write
        // (the in-memory Editor is still updated instantly); FlushEditorSettings() lands it on close.
        Editor = EditorOptions.FromSettings(_settings.Current.Editor);
        _editorSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _editorSaveTimer.Tick += (_, _) => FlushEditorSettings();
        Editor.PropertyChanged += (_, _) =>
        {
            _editorDirty = true;
            _editorSaveTimer.Stop();
            _editorSaveTimer.Start();
        };

        // Shared shell-layout options, same restore-and-persist pattern. Drives the chrome in later phases.
        Layout = LayoutOptions.FromSettings(_settings.Current.Layout);
        Layout.PropertyChanged += (_, _) =>
            _settings.Update(_settings.Current with { Layout = Layout.ToSettings() });

        Tabs.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTabs));
            SyncWatchedPaths();
        };
        _theme.Changed += (_, _) => OnPropertyChanged(nameof(CurrentTheme));
        _recent.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRecent));
            RefreshRecentItems();
        };
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
        };

        if (SelectedTab is { IsMarkdown: true } tab)
        {
            items.Add(new PaletteItem("Переключить предпросмотр / исходник", tab.ToggleViewModeCommand));
            items.Add(new PaletteItem("Экспорт в HTML…", ExportHtmlCommand));
        }

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
                AddTab(DocumentTabViewModel.FromLoad(result, path));
            }
            catch
            {
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

    /// <summary>Snapshot of the open file-backed tabs and the active one, for session persistence.</summary>
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
            AddTab(DocumentTabViewModel.FromLoad(result, path));
            _recent.Add(path);
        }
        catch (Exception ex)
        {
            var message = DescribeError(ex, path);
            StatusText = message; // the status bar keeps the message for context...
            ShowError(message);   // ...but the InfoBar is what gets noticed (#28)
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
            Tabs.Remove(other);

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
            Tabs.RemoveAt(i);

        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
            SelectedTab = tab;
    }

    /// <summary>Close every tab (tab context menu).</summary>
    [RelayCommand]
    private void CloseAllTabs()
    {
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
    partial void OnSelectedTabChanged(DocumentTabViewModel? oldValue, DocumentTabViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsActive = false;
        if (newValue is not null)
            newValue.IsActive = true;
    }

    partial void OnSelectedTabChanged(DocumentTabViewModel? value)
    {
        Title = value is null ? "SeriousView" : value.Header + " — SeriousView";
        // Status bar is segmented: the left segment shows messages — the welcome hint when no tab is
        // open, otherwise cleared (the right segment binds the active tab's metrics directly in the
        // view). A read error overwrites this until the next tab change.
        StatusText = value is null ? WelcomeHint : "";
        // The outline pane depends on the active tab's headings.
        OnPropertyChanged(nameof(IsOutlinePaneVisible));
    }
}
