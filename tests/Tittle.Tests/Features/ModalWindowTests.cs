using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Tittle.Shared;
using Xunit;

namespace Tittle.Tests.Features;

public class ModalWindowTests
{
    // An empty ModalWindow has no FluentAvalonia glyphs, so it renders headless (unlike the
    // glyph-bearing modals). Esc must close it — that is the whole behaviour the base centralises.
    [AvaloniaFact]
    public void Escape_ClosesTheWindow()
    {
        var closed = false;
        var window = new ModalWindow();
        window.Closed += (_, _) => closed = true;
        window.Show();

        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Escape,
        });

        Assert.True(closed);
    }

    [AvaloniaFact]
    public void NonEscapeKey_DoesNotClose()
    {
        var closed = false;
        var window = new ModalWindow();
        window.Closed += (_, _) => closed = true;
        window.Show();

        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.A,
        });

        Assert.False(closed);
        window.Close();
    }
}
