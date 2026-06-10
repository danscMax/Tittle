using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SeriousView.Features.Stats;

/// <summary>Document-statistics popover (ported stats panel); DataContext = TextStats.</summary>
public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
