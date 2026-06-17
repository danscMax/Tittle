using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Tittle;
using Tittle.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Tittle.Tests;

/// <summary>
/// Headless Avalonia application for [AvaloniaFact] tests. Uses the real
/// <see cref="App"/> so FluentTheme + ThemeDictionaries resources are available;
/// App's desktop bootstrap is skipped (no classic desktop lifetime in Headless).
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
