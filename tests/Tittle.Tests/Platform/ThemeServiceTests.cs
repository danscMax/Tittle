using System.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Tittle.Core.Abstractions;
using Tittle.Core.Services;
using Tittle.Core.Settings;
using Tittle.Platform;
using Xunit;

namespace Tittle.Tests.Platform;

public class ThemeServiceTests
{
    private static ThemeService NewService(ISettingsStore? store = null)
        => new(new AppSettingsService(store ?? new FakeSettingsStore()));

    [AvaloniaFact]
    public void SetMode_AppliesRequestedThemeVariant()
    {
        var app = Application.Current!;
        var service = NewService();

        service.SetMode(ThemeMode.Light);
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);

        service.SetMode(ThemeMode.Dark);
        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);

        service.SetMode(ThemeMode.Auto);
        Assert.Equal(ThemeVariant.Default, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Cycle_WalksEveryCatalogThemeOnceThenWraps()
    {
        var service = NewService(); // starts Dark == ThemeCatalog.All[0]

        var visited = new System.Collections.Generic.List<ThemeMode> { service.Mode };
        for (var i = 0; i < ThemeCatalog.All.Count - 1; i++)
        {
            service.Cycle();
            visited.Add(service.Mode);
        }

        // Every catalog mode visited exactly once…
        Assert.Equal(
            ThemeCatalog.All.Select(t => t.Mode).OrderBy(m => m),
            visited.OrderBy(m => m));

        service.Cycle(); // …and one more wraps back to the first.
        Assert.Equal(ThemeCatalog.All[0].Mode, service.Mode);
    }

    [AvaloniaFact]
    public void SetMode_PersistsThemeToSettings()
    {
        var holder = new AppSettingsService(new FakeSettingsStore());
        var service = new ThemeService(holder);

        service.SetMode(ThemeMode.Light);

        Assert.Equal(ThemeMode.Light, holder.Current.Theme);
    }

    [Fact]
    public void Ctor_RestoresPersistedMode()
    {
        var store = new FakeSettingsStore();
        new AppSettingsService(store).Update(new AppSettings { Theme = ThemeMode.Auto });

        Assert.Equal(ThemeMode.Auto, NewService(store).Mode);
    }

    [Fact]
    public void Next_FollowsTheCatalogOrderAndWraps()
    {
        // The shared cycle helper (reused by ThemeService and FakeThemeService) is catalog-driven.
        for (var i = 0; i < ThemeCatalog.All.Count; i++)
        {
            var current = ThemeCatalog.All[i].Mode;
            var expected = ThemeCatalog.All[(i + 1) % ThemeCatalog.All.Count].Mode;
            Assert.Equal(expected, current.Next());
        }
    }

    [Fact]
    public void IsDark_MatchesTheCatalogFlagForEveryMode()
    {
        foreach (var theme in ThemeCatalog.All)
            Assert.Equal(theme.IsDark, theme.Mode.IsDark());

        // Spot-check the two non-dark anchors.
        Assert.False(ThemeMode.Light.IsDark());
        Assert.False(ThemeMode.Auto.IsDark());
        Assert.True(ThemeMode.Dark.IsDark());
    }

    [Fact]
    public void Catalog_HasOneEntryPerThemeMode_NoDuplicates()
    {
        var modes = ThemeCatalog.All.Select(t => t.Mode).ToList();
        Assert.Equal(modes.Count, modes.Distinct().Count());
        // Every enum member is represented in the gallery/cycle.
        foreach (ThemeMode mode in System.Enum.GetValues(typeof(ThemeMode)))
            Assert.Contains(mode, modes);
    }

    [AvaloniaFact]
    public void DarkVariants_OverrideSurfaces_AndInheritTheRest()
    {
        var app = Application.Current!;
        var service = NewService();

        service.SetMode(ThemeMode.Midnight);
        Assert.Equal(AppThemeVariants.Midnight, app.RequestedThemeVariant);

        // Overridden surface token resolves to the Midnight value…
        Assert.True(app.TryGetResource("WindowBackgroundBrush", AppThemeVariants.Midnight, out var bg));
        Assert.Equal(Color.Parse("#0A0A0E"), ((ISolidColorBrush)bg!).Color);

        // …while an un-overridden token falls back to the inherited Dark dictionary.
        Assert.True(app.TryGetResource("AdmonitionNoteBrush", AppThemeVariants.Midnight, out var note));
        Assert.Equal(Color.Parse("#4493F8"), ((ISolidColorBrush)note!).Color);

        service.SetMode(ThemeMode.Ocean);
        Assert.Equal(AppThemeVariants.Ocean, app.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void PortedVariants_ResolveTheirOwnSurface_AndInheritTheBase()
    {
        var app = Application.Current!;
        var service = NewService();

        // A ported dark variant (Nord) renders its own background…
        service.SetMode(ThemeMode.Nord);
        Assert.Equal(AppThemeVariants.Nord, app.RequestedThemeVariant);
        Assert.True(app.TryGetResource("WindowBackgroundBrush", AppThemeVariants.Nord, out var nordBg));
        Assert.Equal(Color.Parse("#272B34"), ((ISolidColorBrush)nordBg!).Color); // F-11: darkened for code contrast
        // …while an un-overridden token falls back to the inherited Dark dictionary.
        Assert.True(app.TryGetResource("AdmonitionNoteBrush", AppThemeVariants.Nord, out var nordNote));
        Assert.Equal(Color.Parse("#4493F8"), ((ISolidColorBrush)nordNote!).Color);

        // A ported light variant (Sepia) renders its own background and inherits the Light base.
        service.SetMode(ThemeMode.Sepia);
        Assert.Equal(AppThemeVariants.Sepia, app.RequestedThemeVariant);
        Assert.True(app.TryGetResource("WindowBackgroundBrush", AppThemeVariants.Sepia, out var sepiaBg));
        Assert.Equal(Color.Parse("#F7F1E1"), ((ISolidColorBrush)sepiaBg!).Color);
        Assert.True(app.TryGetResource("AdmonitionNoteBrush", AppThemeVariants.Sepia, out var sepiaNote));
        Assert.Equal(Color.Parse("#0969DA"), ((ISolidColorBrush)sepiaNote!).Color); // Light.axaml value
    }

    [AvaloniaFact]
    public void EveryThemeMode_AppliesAResolvableVariant()
    {
        var app = Application.Current!;
        var service = NewService();

        foreach (var theme in ThemeCatalog.All)
        {
            if (theme.Mode == ThemeMode.Auto)
                continue; // Auto → Default (follow-OS); no concrete dictionary to resolve headless.

            service.SetMode(theme.Mode);
            // Every concrete theme must resolve its own palette — none silently maps to OS-follow.
            Assert.True(
                app.TryGetResource("WindowBackgroundBrush", app.RequestedThemeVariant, out var bg),
                $"{theme.Mode} did not resolve WindowBackgroundBrush");
            Assert.NotNull(bg);
        }
    }

    [AvaloniaFact]
    public void ColorTokens_ResolveDifferentlyPerVariant()
    {
        var app = Application.Current!;

        Assert.True(app.TryGetResource("WindowBackgroundBrush", ThemeVariant.Dark, out var dark));
        Assert.True(app.TryGetResource("WindowBackgroundBrush", ThemeVariant.Light, out var light));

        var darkColor = ((ISolidColorBrush)dark!).Color;
        var lightColor = ((ISolidColorBrush)light!).Color;

        Assert.NotEqual(darkColor, lightColor);
        Assert.Equal(Color.Parse("#15151A"), darkColor);
        Assert.Equal(Color.Parse("#F7F7FB"), lightColor);
    }

    // Every catalog swatch mirrors its AXAML palette: Background == WindowBackgroundBrush exactly,
    // and Surface is one of the theme's real surface tokens — EditorSurfaceBrush, or the
    // SidebarSurfaceBrush mid-tone for the warm light themes whose editor surface ≈ the background
    // (so the sidebar tone is the distinguishable preview chip). Asserting "one of the two real
    // surfaces" catches a stale swatch (the F-11 surface tweak left a value matching neither) without
    // over-constraining which representative surface each theme picked.
    [AvaloniaFact]
    public void AllSwatches_MatchTheirAxamlSurfaces()
    {
        var app = Application.Current!;
        var service = NewService();

        foreach (var info in ThemeCatalog.All)
        {
            if (info.Mode == ThemeMode.Auto)
                continue; // Auto → OS-follow; no concrete dictionary, swatch is a synthetic split.

            service.SetMode(info.Mode);
            var variant = app.RequestedThemeVariant!;

            Assert.True(app.TryGetResource("WindowBackgroundBrush", variant, out var bg));
            Assert.Equal(Color.Parse(info.Background), ((ISolidColorBrush)bg!).Color);

            var editor = Resolve(app, "EditorSurfaceBrush", variant);
            var sidebar = Resolve(app, "SidebarSurfaceBrush", variant);
            var swatch = Color.Parse(info.Surface);
            Assert.True(swatch == editor || swatch == sidebar,
                $"{info.Mode}: surface swatch {info.Surface} matches neither EditorSurface ({editor}) nor SidebarSurface ({sidebar})");
        }
    }

    // Accessibility floor: muted chrome text (on the sidebar) and status-bar text (on its own bar)
    // must clear WCAG AA 4.5:1 in every concrete theme — secondary text still conveys information.
    [AvaloniaFact]
    public void EveryTheme_MutedAndStatusText_ClearWcagAa()
    {
        var app = Application.Current!;
        var service = NewService();

        foreach (var theme in ThemeCatalog.All)
        {
            if (theme.Mode == ThemeMode.Auto)
                continue;

            service.SetMode(theme.Mode);
            var v = app.RequestedThemeVariant!;

            var muted = Resolve(app, "ChromeForegroundMutedBrush", v);
            var side = Resolve(app, "SidebarSurfaceBrush", v);
            var statusFg = Resolve(app, "StatusBarForegroundBrush", v);
            var statusBg = Resolve(app, "StatusBarBackgroundBrush", v);

            Assert.True(Contrast(muted, side) >= 4.5,
                $"{theme.Mode}: muted text contrast {Contrast(muted, side):F2} < 4.5");
            Assert.True(Contrast(statusFg, statusBg) >= 4.5,
                $"{theme.Mode}: status text contrast {Contrast(statusFg, statusBg):F2} < 4.5");
        }
    }

    private static Color Resolve(Application app, string key, ThemeVariant variant)
    {
        Assert.True(app.TryGetResource(key, variant, out var res), $"{key} unresolved for {variant}");
        return ((ISolidColorBrush)res!).Color;
    }

    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color c)
            => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        var l1 = Luminance(a);
        var l2 = Luminance(b);
        if (l2 > l1)
            (l1, l2) = (l2, l1);
        return (l1 + 0.05) / (l2 + 0.05);
    }
}
