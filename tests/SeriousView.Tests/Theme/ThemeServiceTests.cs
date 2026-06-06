using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using SeriousView.Services;
using Xunit;

namespace SeriousView.Tests.Theme;

public class ThemeServiceTests
{
    [AvaloniaFact]
    public void Toggle_FlipsRequestedThemeVariant()
    {
        var app = Application.Current!;
        app.RequestedThemeVariant = ThemeVariant.Dark;
        var service = new ThemeService();

        service.Toggle();
        Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);

        service.Toggle();
        Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
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
