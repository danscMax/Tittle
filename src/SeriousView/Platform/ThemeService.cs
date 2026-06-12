using System;
using Avalonia;
using Avalonia.Styling;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>The custom dark variants (ported DARK_THEMES). Each inherits
/// <see cref="ThemeVariant.Dark"/>, so its palette only overrides the surface tokens —
/// every other key falls back to the Dark dictionary.</summary>
public static class AppThemeVariants
{
    public static ThemeVariant Midnight { get; } = new("Midnight", ThemeVariant.Dark);

    public static ThemeVariant Ocean { get; } = new("Ocean", ThemeVariant.Dark);

    public static ThemeVariant DeepBlue { get; } = new("DeepBlue", ThemeVariant.Dark);
}

/// <summary>
/// Maps <see cref="ThemeMode"/> to <see cref="Application.RequestedThemeVariant"/>.
/// Auto → <see cref="ThemeVariant.Default"/>, which makes Avalonia follow the OS
/// theme (and react to OS theme changes) automatically. The chosen mode is restored from
/// <see cref="IAppSettingsService"/> on construction and persisted on every change.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly IAppSettingsService _settings;

    public ThemeService(IAppSettingsService settings)
    {
        _settings = settings;
        Mode = settings.Current.Theme; // restore persisted mode (applied at startup via ApplyCurrent)
    }

    public ThemeMode Mode { get; private set; }

    public event EventHandler? Changed;

    public void SetMode(ThemeMode mode)
    {
        Mode = mode;
        _settings.Update(_settings.Current with { Theme = mode });
        Apply();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cycle() => SetMode(Mode.Next());

    /// <summary>Applies the current <see cref="Mode"/> to the app <em>without</em> persisting —
    /// called once at startup so the saved theme is in place before the first render (no flash).</summary>
    public void ApplyCurrent() => Apply();

    private void Apply()
    {
        if (Application.Current is not { } app)
            return;

        app.RequestedThemeVariant = Mode switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Midnight => AppThemeVariants.Midnight,
            ThemeMode.Ocean => AppThemeVariants.Ocean,
            ThemeMode.DeepBlue => AppThemeVariants.DeepBlue,
            _ => ThemeVariant.Default, // follow OS
        };
    }
}
