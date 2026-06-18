using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Tittle;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Tittle.Platform;

// Layer-1 render oracle: render leaf controls to PNG in every theme (the cheap, deterministic
// both-theme visual check). Output dir is arg[0] (default: plans/avalonia-smoke/screenshots).
// Add screen builders to SCREENS; never build MainWindow (chrome Symbols-font crash under Skia).

var outDir = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "plans", "avalonia-smoke", "screenshots");
outDir = Path.GetFullPath(outDir);
Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .WithBundledInterFont() // the preview style references fonts:Inter#Inter — register it here too
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// (theme name, ThemeVariant) — render every one. Base Light/Dark catch most theme regressions;
// add the custom variants (AppThemeVariants.DeepBlue, …) here if a palette needs eyes.
var themes = new (string Name, ThemeVariant Variant)[]
{
    ("dark", ThemeVariant.Dark),
    ("light", ThemeVariant.Light),
    // Custom variants ported from the original viewer (commit 3833f54) — never visually audited.
    ("midnight", AppThemeVariants.Midnight),
    ("ocean", AppThemeVariants.Ocean),
    ("deepblue", AppThemeVariants.DeepBlue),
    ("nord", AppThemeVariants.Nord),
    ("dracula", AppThemeVariants.Dracula),
    ("solarizeddark", AppThemeVariants.SolarizedDark),
    ("solarizeddim", AppThemeVariants.SolarizedDim),
    ("gruvboxdark", AppThemeVariants.GruvboxDark),
    ("highcontrast", AppThemeVariants.HighContrast),
    ("sepia", AppThemeVariants.Sepia),
    ("solarizedlight", AppThemeVariants.SolarizedLight),
    ("gruvboxlight", AppThemeVariants.GruvboxLight),
};

// (screen name, builder). Leaf controls only. Each builder is hermetic (no file/network side effects
// beyond reading a sample doc). Realistic size (>=900px wide) so nothing falsely truncates.
var sampleMd = File.ReadAllText(Path.Combine(outDir, "..", "..", "ux-audit", "rich.md"));
var screens = new (string Name, Func<Control> Build)[]
{
    ("docview", () => new DocumentView { DataContext = DocumentTabViewModel.FromFile(sampleMd, @"E:\docs\rich.md") }),
};

int ok = 0, total = 0;
Dispatcher.UIThread.Invoke(() =>
{
    foreach (var (themeName, variant) in themes)
    {
        Application.Current!.RequestedThemeVariant = variant;
        foreach (var (screenName, build) in screens)
        {
            total++;
            var name = $"{screenName}__{themeName}.png";
            try
            {
                var window = new Window { Width = 960, Height = 720, Content = build() };
                window.Show();
                for (var i = 0; i < 6; i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(80); }
                var frame = window.CaptureRenderedFrame();
                window.Close();
                if (frame is null) { Console.WriteLine($"FAIL {name}: null frame"); continue; }
                frame.Save(Path.Combine(outDir, name));
                Console.WriteLine($"  ok  {name} ({frame.PixelSize.Width}x{frame.PixelSize.Height})");
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL {name}: {ex.GetType().Name} {ex.Message}");
            }
        }
    }
});

Console.WriteLine($"{ok}/{total} rendered -> {outDir}");
return ok == total ? 0 : 1;
