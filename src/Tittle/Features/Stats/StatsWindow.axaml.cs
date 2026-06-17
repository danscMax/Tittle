using Avalonia.Interactivity;
using Tittle.Shared;

namespace Tittle.Features.Stats;

/// <summary>Document-statistics popover (ported stats panel); DataContext = TextStats.
/// Esc-close comes from <see cref="ModalWindow"/>.</summary>
public partial class StatsWindow : ModalWindow
{
    public StatsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
