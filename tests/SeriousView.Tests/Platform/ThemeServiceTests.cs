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
    public void Cycle_GoesDarkLightAutoDark()
    {
        var service = NewService(); // starts Dark

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
    public void Next_CyclesDarkLightAutoDark()
    {
        // The shared cycle helper reused by ThemeService and FakeThemeService.
        Assert.Equal(ThemeMode.Light, ThemeMode.Dark.Next());
        Assert.Equal(ThemeMode.Auto, ThemeMode.Light.Next());
        Assert.Equal(ThemeMode.Dark, ThemeMode.Auto.Next());
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
