using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;
    private readonly IThemeService _theme;
    private readonly IRecentFilesStore _recent;

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

    public MainWindowViewModel(
        IFileDialogService fileDialog, IFileReader fileReader, IThemeService theme,
        IRecentFilesStore recent, string[] args)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;
        _theme = theme;
        _recent = recent;

        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
        _theme.Changed += (_, _) => OnPropertyChanged(nameof(ThemeModeLabel));
        _recent.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(RecentFiles));
            OnPropertyChanged(nameof(HasRecent));
        };

        // Open a file passed on the command line — asynchronously and guarded, so a
        // missing/locked/unreadable file can't crash the app on startup.
        var startupPath = args.Length > 0 ? args[0] : null;
        if (startupPath is not null)
            _ = OpenPathAsync(startupPath);
        // Otherwise no tab is opened — the welcome view is shown while HasTabs is false.
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
