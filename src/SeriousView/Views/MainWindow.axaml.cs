using System;
using Avalonia;                 // AttachDevTools extension lives in the Avalonia namespace
using Avalonia.Controls;
using Avalonia.Media;
using SeriousView.ViewModels;

namespace SeriousView.Views;

public partial class MainWindow : Window
{
    // Parameterless ctor for the XAML designer.
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    public MainWindow(MainWindowViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // No OS blur (older Windows / some Linux): Background=Transparent would show the
        // desktop, so fall back to a solid window background.
        if (ActualTransparencyLevel == WindowTransparencyLevel.None
            && this.TryFindResource("WindowBackgroundBrush", out var res) && res is IBrush solid)
        {
            Background = solid;
        }
    }
}
