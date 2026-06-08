using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;
    private readonly IThemeService _theme;
    private readonly IRecentFilesStore _recent;
    private readonly IAppSettingsService _settings;

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    /// <summary>Recent file paths (surfaced by the welcome view).</summary>
    public IReadOnlyList<string> RecentFiles => _recent.Items;

    public bool HasRecent => _recent.Items.Count > 0;

    /// <summary>True when at least one document tab is open (drives the empty placeholder).</summary>
    public bool HasTabs => Tabs.Count > 0;

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _title = "SeriousView";

    /// <summary>Status bar text — mirrors the active tab.</summary>
    [ObservableProperty]
    private string _statusText = "Готово";

    /// <summary>Whether the user has the outline pane turned on (per-window, persists across tabs).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlinePaneVisible))]
    private bool _isOutlineVisible = true;

    /// <summary>The outline pane is shown only when enabled AND the active tab has headings.</summary>
    public bool IsOutlinePaneVisible => IsOutlineVisible && (SelectedTab?.HasOutline ?? false);

    [RelayCommand]
    private void ToggleOutline() => IsOutlineVisible = !IsOutlineVisible;

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

    public MainWindowViewModel(
        IFileDialogService fileDialog, IFileReader fileReader, IThemeService theme,
        IRecentFilesStore recent, IAppSettingsService settings, string[] args)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;
        _theme = theme;
        _recent = recent;
        _settings = settings;

        // Shared editor options, restored from settings and persisted on every change.
        Editor = EditorOptions.FromSettings(_settings.Current.Editor);
        Editor.PropertyChanged += (_, _) =>
            _settings.Update(_settings.Current with { Editor = Editor.ToSettings() });

        // Shared shell-layout options, same restore-and-persist pattern. Drives the chrome in later phases.
        Layout = LayoutOptions.FromSettings(_settings.Current.Layout);
        Layout.PropertyChanged += (_, _) =>
            _settings.Update(_settings.Current with { Layout = Layout.ToSettings() });

        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
        _theme.Changed += (_, _) => OnPropertyChanged(nameof(ThemeModeLabel));
        _recent.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(RecentFiles));
            OnPropertyChanged(nameof(HasRecent));
        };

        // Startup precedence: an explicit file argument wins, then the last session, else welcome.
        // All paths are async and guarded so a missing/locked/unreadable file can't crash startup.
        if (args.Length > 0)
            _ = OpenPathAsync(args[0]);
        else if (_settings.Current.Session is { OpenFiles.Count: > 0 } session)
            _ = RestoreSessionAsync(session);
        // Otherwise no tab is opened — the welcome view is shown while HasTabs is false.
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

    /// <summary>Opens the built-in sample document (offered on the welcome screen).</summary>
    [RelayCommand]
    private void OpenSample() => AddTab(DocumentTabViewModel.CreateSample());

    /// <summary>Loads <paramref name="path"/> into a new active tab and records it as recent.
    /// Real I/O failures become a friendly status message instead of a crash.</summary>
    public async Task OpenPathAsync(string path)
    {
        try
        {
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

    /// <summary>Label for the theme button, reflecting the current mode.</summary>
    public string ThemeModeLabel => _theme.Mode switch
    {
        ThemeMode.Light => "Светлая",
        ThemeMode.Auto => "Авто",
        _ => "Тёмная",
    };

    [RelayCommand]
    private void ToggleTheme() => _theme.Cycle();

    private void AddTab(DocumentTabViewModel tab)
    {
        tab.Editor = Editor; // share one editor-options instance across all tabs
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    partial void OnSelectedTabChanged(DocumentTabViewModel? value)
    {
        Title = value is null ? "SeriousView" : value.Header + " — SeriousView";
        // Status bar is segmented: the left segment shows messages (reset to idle when no
        // tab is open), the right segment binds the active tab's metrics
        // (DocumentTabViewModel.StatusText) directly in the view.
        if (value is null)
            StatusText = "Готово";
        // The outline pane depends on the active tab's headings.
        OnPropertyChanged(nameof(IsOutlinePaneVisible));
    }
}
