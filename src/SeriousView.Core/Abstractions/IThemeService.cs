using System;

namespace SeriousView.Core.Abstractions;

/// <summary>Light, dark, or follow the OS.</summary>
public enum ThemeMode
{
    Dark,
    Light,
    Auto,
}

/// <summary>Controls the application theme variant.</summary>
public interface IThemeService
{
    ThemeMode Mode { get; }

    void SetMode(ThemeMode mode);

    /// <summary>Cycles Dark → Light → Auto → Dark.</summary>
    void Cycle();

    event EventHandler? Changed;
}
