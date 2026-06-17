using System;

namespace Tittle.Core.Abstractions;

/// <summary>Light, one of the dark/light set (ported from the original viewer), or follow the OS.
/// Serialized by name (see the JSON enum converter), so members may be appended freely — the order
/// of the cycle and the gallery lives in <see cref="ThemeCatalog"/>, not in this declaration.</summary>
public enum ThemeMode
{
    Dark,
    Light,
    Auto,
    Midnight,
    Ocean,
    DeepBlue,
    Nord,
    Dracula,
    SolarizedDark,
    SolarizedDim,
    GruvboxDark,
    HighContrast,
    Sepia,
    SolarizedLight,
    GruvboxLight,
}

/// <summary>Helpers for <see cref="ThemeMode"/>, both driven by <see cref="ThemeCatalog"/> so the
/// cycle order and the dark/light split have a single source of truth.</summary>
public static class ThemeModeExtensions
{
    /// <summary>The next mode in the <see cref="ThemeCatalog.All"/> order, wrapping around.</summary>
    public static ThemeMode Next(this ThemeMode mode)
    {
        var all = ThemeCatalog.All;
        for (var i = 0; i < all.Count; i++)
            if (all[i].Mode == mode)
                return all[(i + 1) % all.Count].Mode;
        return all[0].Mode;
    }

    /// <summary>True for every member of the dark family (drives "is dark" decisions
    /// like the HTML-export stylesheet).</summary>
    public static bool IsDark(this ThemeMode mode) => ThemeCatalog.For(mode).IsDark;
}

/// <summary>Controls the application theme variant.</summary>
public interface IThemeService
{
    ThemeMode Mode { get; }

    void SetMode(ThemeMode mode);

    /// <summary>Cycles Dark → Light → Auto → Dark.</summary>
    void Cycle();

    /// <summary>Applies the current <see cref="Mode"/> without persisting — used at startup so the
    /// restored theme is in place before the first render.</summary>
    void ApplyCurrent();

    event EventHandler? Changed;
}
