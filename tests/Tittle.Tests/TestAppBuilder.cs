using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Skia;
using Tittle;
using Tittle.Platform;
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
            // Real font loading: the preview now styles its runs with the bundled fonts:Inter#Inter
            // collection (Avalonia #18875 bold/italic fix). A headless platform with no Skia can't
            // rasterise real fonts, so the preview text throws "Could not create glyphTypeface". Skia +
            // UseHeadlessDrawing=false (same as tools/HeadlessRender) loads the embedded faces for real.
            .UseSkia()
            .WithBundledInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
