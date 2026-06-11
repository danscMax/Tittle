using Avalonia.Controls;
using Avalonia.Input;

namespace SeriousView.Shared;

/// <summary>Base for the small standalone modal windows (Help, Stats, Layout, image lightbox):
/// closes on Esc. Subclasses keep their own close buttons and any extra gestures (e.g. the
/// lightbox also closes on a click). Esc handling lives in <see cref="OnKeyDown"/> rather than a
/// <c>KeyDown +=</c> subscription, so it needs no explicit unsubscribe and runs after the focused
/// child has had its chance at the key.</summary>
public class ModalWindow : Window
{
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }
}
