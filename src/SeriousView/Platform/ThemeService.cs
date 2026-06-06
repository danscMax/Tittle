using System;
using Avalonia;
using Avalonia.Styling;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>
/// Maps <see cref="ThemeMode"/> to <see cref="Application.RequestedThemeVariant"/>.
/// Auto → <see cref="ThemeVariant.Default"/>, which makes Avalonia follow the OS
/// theme (and react to OS theme changes) automatically.
/// </summary>
public sealed class ThemeService : IThemeService
{
    public ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    public event EventHandler? Changed;

    public void SetMode(ThemeMode mode)
    {
        Mode = mode;
        Apply();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cycle() => SetMode(Mode.Next());

    private void Apply()
    {
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant = Mode switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default, // follow OS
        };
    }
}
