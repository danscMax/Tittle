using System.Linq;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;
using SeriousView.Core.Settings;
using SeriousView.Platform;
using Xunit;

namespace SeriousView.Tests.Platform;

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
}
