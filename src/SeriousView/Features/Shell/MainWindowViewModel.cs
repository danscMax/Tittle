using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;
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
    private readonly DispatcherTimer _editorSaveTimer; // coalesces editor-option writes (zoom bursts)
    private bool _editorDirty;

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

    /// <summary>Open the Settings ▸ Layout window — it binds to the shared <see cref="Layout"/>, so its
    /// toggles persist and re-render the chrome live.</summary>
    [RelayCommand]
    private void OpenLayoutSettings() => LayoutSettingsRequested?.Invoke();

    public MainWindowViewModel(
        IFileDialogService fileDialog, IFileReader fileReader, IThemeService theme,
        IRecentFilesStore recent, IAppSettingsService settings, string[] args)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;
        _theme = theme;
        _recent = recent;
        _settings = settings;

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

        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
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
            new("Найти…", OpenSearchCommand, "Ctrl+F"),
            new("Перейти к строке…", OpenGoToLineCommand, "Ctrl+G"),
            new("Перенос строк", ToggleWordWrapCommand, "Alt+Z"),
            new("Номера строк", ToggleLineNumbersCommand, "Ctrl+L"),
            new("Масштаб: больше", ZoomInCommand, "Ctrl++"),
            new("Масштаб: меньше", ZoomOutCommand, "Ctrl+−"),
            new("Масштаб: сбросить", ZoomResetCommand, "Ctrl+0"),
            new("Тема: тёмная", SetThemeCommand, parameter: ThemeMode.Dark),
            new("Тема: светлая", SetThemeCommand, parameter: ThemeMode.Light),
            new("Тема: авто", SetThemeCommand, parameter: ThemeMode.Auto),
            new("Настройки: раскладка…", OpenLayoutSettingsCommand),
        };

        if (SelectedTab is { IsMarkdown: true } tab)
            items.Add(new PaletteItem("Переключить предпросмотр / исходник", tab.ToggleViewModeCommand));

        foreach (var r in RecentItems)
            items.Add(new PaletteItem($"Недавнее: {r.Name}", r.OpenCommand));

        return items;
    }

    /// <summary>Reopens the documents from the saved session, silently skipping any that are
    /// gone/unreadable (no error is surfaced for a restore), then selects the saved tab.</summary>
    private async Task RestoreSessionAsync(SessionState session)
    {
        foreach (var path in session.OpenFiles)
        {
            try
            {
                var result = await _fileReader.LoadAsync(path);
                AddTab(DocumentTabViewModel.FromLoad(result, path));
            }
            catch
            {
                // Skip files that no longer exist or can't be read — restore is best-effort.
            }
        }

        if (Tabs.Count > 0)
            SelectedTab = Tabs[Math.Clamp(session.ActiveIndex, 0, Tabs.Count - 1)];
    }

    /// <summary>Snapshot of the open file-backed tabs and the active one, for session persistence.</summary>
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
        var path = await _fileDialog.PickFileAsync();
        if (path is not null)
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
            StatusText = DescribeError(ex, path);
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
        tab.Editor = Editor;   // share one editor-options instance across all tabs
        tab.Layout = Layout;   // share the shell-layout options (reading mode)
        tab.Shell = this;      // back-reference for the tab's context-menu commands
        Tabs.Add(tab);
        SelectedTab = tab;
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
