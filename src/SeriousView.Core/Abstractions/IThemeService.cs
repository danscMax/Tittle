using System;

namespace SeriousView.Core.Abstractions;

/// <summary>Light, one of the dark set (ported DARK_THEMES), or follow the OS.</summary>
public enum ThemeMode
{
    Dark,
    Light,
    Auto,
    Midnight,
    Ocean,
    DeepBlue,
}

/// <summary>Helpers for <see cref="ThemeMode"/>.</summary>
public static class ThemeModeExtensions
{
    /// <summary>The next mode in the Dark → Midnight → Ocean → Light → Auto → Dark cycle.</summary>
    public static ThemeMode Next(this ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => ThemeMode.Midnight,
        ThemeMode.Midnight => ThemeMode.Ocean,
        ThemeMode.Ocean => ThemeMode.DeepBlue,
        ThemeMode.DeepBlue => ThemeMode.Light,
        ThemeMode.Light => ThemeMode.Auto,
        _ => ThemeMode.Dark,
    };

    /// <summary>True for every member of the dark family (drives "is dark" decisions
    /// like the HTML-export stylesheet).</summary>
    public static bool IsDark(this ThemeMode mode)
        => mode is ThemeMode.Dark or ThemeMode.Midnight or ThemeMode.Ocean or ThemeMode.DeepBlue;
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
