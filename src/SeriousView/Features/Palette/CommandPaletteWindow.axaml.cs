using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SeriousView.Features.Palette;

/// <summary>Top-level Ctrl+K command-palette window. Auto-focuses the query box, navigates with ↑/↓,
/// runs on Enter (or double-click), and closes on Esc / click-away (Deactivated) / after a command runs.</summary>
public partial class CommandPaletteWindow : Window
{
    public CommandPaletteWindow()
    {
        InitializeComponent();
        // Tunnel so ↑/↓/Enter/Esc drive the palette before the focused TextBox/ListBox consume them.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => { Activate(); QueryBox.Focus(); }; // foreground + caret in the query box
        Deactivated += (_, _) => Close(); // click-away dismisses
        // A single click on a result row runs it (the ListBox selects on press, so by the time Tapped
        // fires SelectedIndex is the clicked row). Ignore taps on empty space below the last item.
        ResultsList.Tapped += (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null)
                Vm?.Execute();
        };
    }

    private CommandPaletteViewModel? Vm => DataContext as CommandPaletteViewModel;

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm)
            return;

        switch (e.Key)
        {
            case Key.Down: vm.MoveSelection(1); e.Handled = true; break;
            case Key.Up: vm.MoveSelection(-1); e.Handled = true; break;
            case Key.Enter: vm.Execute(); e.Handled = true; break;
            case Key.Escape: vm.Close(); e.Handled = true; break;
        }
    }
}
