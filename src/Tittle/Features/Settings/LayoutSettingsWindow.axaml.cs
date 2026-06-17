using Avalonia.Interactivity;
using Tittle.Shared;

namespace Tittle.Features.Settings;

/// <summary>Layout customization window (☰ ▸ Раскладка). Bound to the shared LayoutOptions, so every
/// toggle persists and re-renders the chrome live. Stays open while the user watches the changes; closes
/// on Esc (from <see cref="ModalWindow"/>) or the Готово button.</summary>
public partial class LayoutSettingsWindow : ModalWindow
{
    /// <summary>The shared diagram (Kroki) options — bound to the «Диаграммы» section, set when the
    /// window is opened (the window's main DataContext is the LayoutOptions).</summary>
    public DiagramOptions? Diagrams { get; init; }

    public LayoutSettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            Activate();
            DiagramSection.DataContext = Diagrams;
        };
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
