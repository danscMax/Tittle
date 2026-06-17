using Avalonia.Interactivity;
using SeriousView.Shared;

namespace SeriousView.Features.Macros;

/// <summary>Macro manager dialog (☰ Инструменты ▸ Управление макросами). A top-level window like the
/// command palette / layout settings (overlays over AvaloniaEdit don't repaint). Renames and count edits
/// are committed on close (Готово or Esc); deletes commit immediately.</summary>
public partial class MacroManagerWindow : ModalWindow
{
    public MacroManagerWindow()
    {
        InitializeComponent();
        Opened += (_, _) => Activate();
        Closed += (_, _) => (DataContext as MacroManagerViewModel)?.Commit();
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
