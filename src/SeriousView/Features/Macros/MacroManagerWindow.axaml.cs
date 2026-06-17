using Avalonia.Input;
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
        // Tunnel so the shortcut-capture handler sees the combo before the name TextBox or the base
        // Esc-to-close, and can consume it.
        AddHandler(KeyDownEvent, OnCaptureKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();

    // While a row is capturing a shortcut, the next non-modifier key with a Ctrl/Alt modifier becomes its
    // gesture; Esc cancels. The key is consumed so it can't type into the name box or close the dialog.
    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MacroManagerViewModel vm || !vm.IsCapturing)
            return;

        // Wait through modifier-only presses — the user is still assembling the combo.
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        if (e.Key == Key.Escape)
        {
            vm.ApplyCapturedShortcut(null); // cancel
            e.Handled = true;
            return;
        }

        // Require Ctrl or Alt so a macro shortcut can't be a bare key (which would block plain typing).
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            return; // keep waiting for a valid combo

        vm.ApplyCapturedShortcut(new KeyGesture(e.Key, e.KeyModifiers).ToString());
        e.Handled = true;
    }
}
