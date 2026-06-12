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
    public void Cycle_WalksTheWholeSetAndWraps()
    {
        var service = NewService(); // starts Dark

        service.Cycle();
        Assert.Equal(ThemeMode.Midnight, service.Mode);
        service.Cycle();
        Assert.Equal(ThemeMode.Ocean, service.Mode);
        service.Cycle();
        Assert.Equal(ThemeMode.DeepBlue, service.Mode);
        service.Cycle();
        Assert.Equal(ThemeMode.Light, service.Mode);
        service.Cycle();
        Assert.Equal(ThemeMode.Auto, service.Mode);
        service.Cycle();
        Assert.Equal(ThemeMode.Dark, service.Mode);
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
    public void Next_CyclesThroughTheDarkSetThenLightAuto()
    {
        // The shared cycle helper reused by ThemeService and FakeThemeService.
        Assert.Equal(ThemeMode.Midnight, ThemeMode.Dark.Next());
        Assert.Equal(ThemeMode.Ocean, ThemeMode.Midnight.Next());
        Assert.Equal(ThemeMode.DeepBlue, ThemeMode.Ocean.Next());
        Assert.Equal(ThemeMode.Light, ThemeMode.DeepBlue.Next());
        Assert.Equal(ThemeMode.Auto, ThemeMode.Light.Next());
        Assert.Equal(ThemeMode.Dark, ThemeMode.Auto.Next());
    }

    [Fact]
    public void IsDark_CoversTheWholeDarkFamily()
    {
        Assert.True(ThemeMode.Dark.IsDark());
        Assert.True(ThemeMode.Midnight.IsDark());
        Assert.True(ThemeMode.Ocean.IsDark());
        Assert.True(ThemeMode.DeepBlue.IsDark());
        Assert.False(ThemeMode.Light.IsDark());
        Assert.False(ThemeMode.Auto.IsDark());
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
