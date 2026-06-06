using Avalonia;
using Avalonia.Styling;
using SeriousView.Core.Abstractions;

namespace SeriousView.Services;

/// <summary>Flips <see cref="Application.RequestedThemeVariant"/> between Light and Dark.</summary>
public sealed class ThemeService : IThemeService
{
    public void Toggle()
    {
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant =
            app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
