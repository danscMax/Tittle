using System;

namespace SeriousView.Core.Abstractions;

/// <summary>Light, dark, or follow the OS.</summary>
public enum ThemeMode
{
    Dark,
    Light,
    Auto,
}

/// <summary>Helpers for <see cref="ThemeMode"/>.</summary>
public static class ThemeModeExtensions
{
    /// <summary>The next mode in the Dark → Light → Auto → Dark cycle.</summary>
    public static ThemeMode Next(this ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => ThemeMode.Light,
        ThemeMode.Light => ThemeMode.Auto,
        _ => ThemeMode.Dark,
    };
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
