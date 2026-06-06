using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Abstractions;

namespace SeriousView.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _fileDialog;
    private readonly IFileReader _fileReader;
    private readonly IThemeService _theme;

    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = new();

    /// <summary>True when at least one document tab is open (drives the empty placeholder).</summary>
    public bool HasTabs => Tabs.Count > 0;

    [ObservableProperty]
    private DocumentTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _title = "SeriousView";

    /// <summary>Status bar text — mirrors the active tab.</summary>
    [ObservableProperty]
    private string _statusText = "Готово";

    public MainWindowViewModel(
        IFileDialogService fileDialog, IFileReader fileReader, IThemeService theme, string[] args)
    {
        _fileDialog = fileDialog;
        _fileReader = fileReader;
        _theme = theme;

        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));

        var startupPath = args.Length > 0 ? args[0] : null;
        if (startupPath is not null && _fileReader.Exists(startupPath))
            AddTab(DocumentTabViewModel.FromFile(_fileReader.ReadAllText(startupPath), startupPath));
        else
            AddTab(DocumentTabViewModel.CreateSample());
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var path = await _fileDialog.PickFileAsync();
        if (path is null)
            return;

        try
        {
            var text = await _fileReader.ReadAllTextAsync(path);
            AddTab(DocumentTabViewModel.FromFile(text, path));
        }
        catch (Exception ex)
        {
            StatusText = "Ошибка чтения: " + ex.Message;
        }
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

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    private void AddTab(DocumentTabViewModel tab)
    {
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    partial void OnSelectedTabChanged(DocumentTabViewModel? value)
    {
        Title = value is null ? "SeriousView" : value.Header + " — SeriousView";
        StatusText = value?.StatusText ?? "Готово";
    }
}
