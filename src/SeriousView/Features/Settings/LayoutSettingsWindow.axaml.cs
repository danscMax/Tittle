using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace SeriousView.Features.Settings;

/// <summary>Layout customization window (☰ ▸ Раскладка). Bound to the shared LayoutOptions, so every
/// toggle persists and re-renders the chrome live. Stays open while the user watches the changes; closes
/// on Esc or the Готово button.</summary>
public partial class LayoutSettingsWindow : Window
{
    public LayoutSettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
