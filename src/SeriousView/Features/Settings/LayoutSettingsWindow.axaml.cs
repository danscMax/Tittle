using Avalonia.Interactivity;
using SeriousView.Shared;

namespace SeriousView.Features.Settings;

/// <summary>Layout customization window (☰ ▸ Раскладка). Bound to the shared LayoutOptions, so every
/// toggle persists and re-renders the chrome live. Stays open while the user watches the changes; closes
/// on Esc (from <see cref="ModalWindow"/>) or the Готово button.</summary>
public partial class LayoutSettingsWindow : ModalWindow
{
    public LayoutSettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Activate();
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
