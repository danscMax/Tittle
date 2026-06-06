using CommunityToolkit.Mvvm.ComponentModel;

namespace SeriousView.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>OS window title.</summary>
    [ObservableProperty]
    private string _title = "SeriousView";

    /// <summary>Top header line (current file / hint).</summary>
    [ObservableProperty]
    private string _headerText = "SeriousView";

    /// <summary>Bottom status bar text.</summary>
    [ObservableProperty]
    private string _statusText = "Готово";
}
