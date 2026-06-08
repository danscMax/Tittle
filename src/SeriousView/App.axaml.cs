using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;
using SeriousView.Platform;
using SeriousView.Features.Shell;

namespace SeriousView;

public partial class App : Application
{
    /// <summary>Composition root, available after framework initialization.</summary>
    public IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = ConfigureServices(desktop.Args ?? Array.Empty<string>());
            // Apply the persisted theme before the window is created — no flash of the wrong theme.
            Services.GetRequiredService<IThemeService>().ApplyCurrent();
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();

            // Single-instance: route file opens forwarded from secondary launches into this window.
            WireSingleInstance(Program.Gate, desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Wire the single-instance gate's pipe server to the live window: each forwarded path
    /// opens as a new tab (the existing <see cref="MainWindowViewModel.OpenPathAsync"/> seam, already
    /// crash-guarded and used by drag-drop) and the window is brought to the front.</summary>
    private void WireSingleInstance(SingleInstanceGate? gate, IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (gate is null || Services is null)
            return;

        var vm = Services.GetRequiredService<MainWindowViewModel>();
        gate.FileOpenRequested += paths =>
        {
            // Raised on the pipe background thread — hop to the UI thread for all VM/window work.
            Dispatcher.UIThread.Post(async () =>
            {
                foreach (var path in paths)
                    await vm.OpenPathAsync(path);
                BringToFront(desktop.MainWindow); // activate even when paths was empty (no-arg launch)
            });
        };
        gate.StartServer();

        // Deterministic teardown on the normal close path (Program's finally is the backstop).
        desktop.ShutdownRequested += (_, _) => gate.Dispose();
    }

    /// <summary>Bring the existing window forward for a forwarded open: restore if minimized, Activate(),
    /// then on Windows a brief Topmost flip to defeat the foreground-stealing guard (dropped at once).</summary>
    private static void BringToFront(Window? window)
    {
        if (window is null)
            return;
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        if (OperatingSystem.IsWindows())
        {
            window.Topmost = true;
            window.Topmost = false;
        }
    }

    private static ServiceProvider ConfigureServices(string[] args)
    {
        var services = new ServiceCollection();

        // Startup command-line args (e.g. a file path) as a DI-resolvable value.
        services.AddSingleton(args);

        // Core services (pure, testable).
        services.AddSingleton<IFileReader, FileReader>();

        // UI services. The picker needs the active TopLevel — resolved lazily so it
        // is available after the window is shown (avoids a construction cycle).
        services.AddSingleton<Func<TopLevel?>>(_ => static () =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow);
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IRecentFilesStore, RecentFilesStore>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
