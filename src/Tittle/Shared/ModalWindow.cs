using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Tittle.Shared;

/// <summary>Base for the small standalone modal windows (Help, Stats, Layout, Macros, Donate, image
/// lightbox, command palette). Owns the shared transparent-chrome window shape (no system decorations,
/// transparent backing, centred on the owner, hidden from the taskbar) so each window's XAML carries only
/// what differs (size, title, content). The rounded card surface is the shared <c>Border.modalcard</c>
/// style in <c>Themes/Controls.axaml</c>. Closes on Esc — handled in <see cref="OnKeyDown"/> rather than a
/// <c>KeyDown +=</c> subscription, so it needs no unsubscribe and runs after the focused child.</summary>
public class ModalWindow : Window
{
    public ModalWindow()
    {
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    /// <summary>Shared handler for the close / «Готово» button (<c>Click="OnCloseClick"</c>), so each
    /// window no longer repeats a one-line <c>=&gt; Close()</c>.</summary>
    protected void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
