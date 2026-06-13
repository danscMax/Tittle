using System;
using Avalonia;
using Avalonia.Styling;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>The custom theme variants ported from the original viewer. Each inherits either
/// <see cref="ThemeVariant.Dark"/> or <see cref="ThemeVariant.Light"/>, so its palette only
/// overrides the surface/chrome/accent tokens — every other key falls back to the base dictionary.</summary>
public static class AppThemeVariants
{
    // Dark family (inherit Dark)
    public static ThemeVariant Midnight { get; } = new("Midnight", ThemeVariant.Dark);

    public static ThemeVariant Ocean { get; } = new("Ocean", ThemeVariant.Dark);

    public static ThemeVariant DeepBlue { get; } = new("DeepBlue", ThemeVariant.Dark);

    public static ThemeVariant Nord { get; } = new("Nord", ThemeVariant.Dark);

    public static ThemeVariant Dracula { get; } = new("Dracula", ThemeVariant.Dark);

    public static ThemeVariant SolarizedDark { get; } = new("SolarizedDark", ThemeVariant.Dark);

    public static ThemeVariant SolarizedDim { get; } = new("SolarizedDim", ThemeVariant.Dark);

    public static ThemeVariant GruvboxDark { get; } = new("GruvboxDark", ThemeVariant.Dark);

    // NB: the variant Key must NOT be "HighContrast" — that string collides with the platform/
    // FluentAvalonia high-contrast handling (Markdown.Avalonia's auto FluentAvalonia style then
    // resolves a LIGHT high-contrast base instead of this variant's inherited Dark, so the preview
    // body renders light under a black-chrome accessibility theme). "ContrastDark" sidesteps it.
    // The Key is internal only — persistence uses ThemeMode.HighContrast, the label comes from
    // ThemeCatalog, and resource wiring is by x:Static object reference — so the string is free.
    public static ThemeVariant HighContrast { get; } = new("ContrastDark", ThemeVariant.Dark);

    // Light family (inherit Light)
    public static ThemeVariant Sepia { get; } = new("Sepia", ThemeVariant.Light);

    public static ThemeVariant SolarizedLight { get; } = new("SolarizedLight", ThemeVariant.Light);

    public static ThemeVariant GruvboxLight { get; } = new("GruvboxLight", ThemeVariant.Light);
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
            ThemeMode.Nord => AppThemeVariants.Nord,
            ThemeMode.Dracula => AppThemeVariants.Dracula,
            ThemeMode.SolarizedDark => AppThemeVariants.SolarizedDark,
            ThemeMode.SolarizedDim => AppThemeVariants.SolarizedDim,
            ThemeMode.GruvboxDark => AppThemeVariants.GruvboxDark,
            ThemeMode.HighContrast => AppThemeVariants.HighContrast,
            ThemeMode.Sepia => AppThemeVariants.Sepia,
            ThemeMode.SolarizedLight => AppThemeVariants.SolarizedLight,
            ThemeMode.GruvboxLight => AppThemeVariants.GruvboxLight,
            _ => ThemeVariant.Default, // follow OS
        };
    }
}
