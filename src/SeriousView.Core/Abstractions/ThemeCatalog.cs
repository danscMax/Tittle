using System.Collections.Generic;

namespace SeriousView.Core.Abstractions;

/// <summary>One entry in the theme gallery: a <see cref="ThemeMode"/> plus the metadata the picker
/// needs — display name, dark/light family, and four representative swatch colours (hex) for the
/// preview tile. The swatch colours mirror the theme's AXAML palette
/// (background · surface · accent · foreground).</summary>
public sealed record ThemeInfo(
    ThemeMode Mode,
    string DisplayName,
    bool IsDark,
    string Background,
    string Surface,
    string Accent,
    string Foreground);

/// <summary>The single source of truth for the theme set: defines the order of the
/// <see cref="ThemeModeExtensions.Next"/> cycle, the gallery layout, and the per-mode
/// <see cref="ThemeModeExtensions.IsDark"/> answer. Pure (no Avalonia) so both the Core and the
/// platform/UI layers share one list — the swatch hexes here must match the corresponding
/// <c>Themes/Colors/*.axaml</c> palette.</summary>
public static class ThemeCatalog
{
    /// <summary>Dark family first, then the light family, then Auto — this order is the keyboard
    /// cycle and the gallery's reading order.</summary>
    public static IReadOnlyList<ThemeInfo> All { get; } = new ThemeInfo[]
    {
        // Dark family
        new(ThemeMode.Dark, "Тёмная", true, "#15151A", "#1B1B22", "#5A8DFF", "#E7E7EF"),
        new(ThemeMode.Midnight, "Полночь", true, "#0A0A0E", "#0F0F14", "#7AA2F7", "#DEDEE9"),
        new(ThemeMode.Ocean, "Океан", true, "#0C141D", "#101B26", "#2EC8BE", "#DCE7EF"),
        new(ThemeMode.DeepBlue, "Глубокий синий", true, "#121A30", "#172139", "#7AA2F7", "#C8D3F5"),
        new(ThemeMode.Nord, "Nord", true, "#2E3440", "#3B4252", "#88C0D0", "#ECEFF4"),
        new(ThemeMode.Dracula, "Dracula", true, "#282A36", "#21222C", "#BD93F9", "#F8F8F2"),
        new(ThemeMode.SolarizedDark, "Solarized Dark", true, "#002B36", "#073642", "#B58900", "#93A1A1"),
        new(ThemeMode.SolarizedDim, "Solarized Dim", true, "#1C2A2F", "#16242A", "#268BD2", "#93A1A1"),
        new(ThemeMode.GruvboxDark, "Gruvbox Dark", true, "#282828", "#32302F", "#FABD2F", "#EBDBB2"),
        new(ThemeMode.HighContrast, "Контраст", true, "#000000", "#0A0A0A", "#FFFF00", "#FFFFFF"),
        // Light family
        new(ThemeMode.Light, "Светлая", false, "#F7F7FB", "#FFFFFF", "#2D6CF6", "#1B1B24"),
        new(ThemeMode.Sepia, "Сепия", false, "#F7F1E1", "#EFE6D0", "#8B4513", "#5B4636"),
        new(ThemeMode.SolarizedLight, "Solarized Light", false, "#FDF6E3", "#EEE8D5", "#268BD2", "#657B83"),
        new(ThemeMode.GruvboxLight, "Gruvbox Light", false, "#FBF1C7", "#F2E5BC", "#AF3A03", "#3C3836"),
        // Follow the OS — neutral split swatch, never counted as dark.
        new(ThemeMode.Auto, "Авто (как в системе)", false, "#15151A", "#F7F7FB", "#5A8DFF", "#9A9AA8"),
    };

    /// <summary>The catalog entry for <paramref name="mode"/> (falls back to the first entry for an
    /// unknown value, which never happens for a valid enum member).</summary>
    public static ThemeInfo For(ThemeMode mode)
    {
        foreach (var info in All)
            if (info.Mode == mode)
                return info;
        return All[0];
    }
}
