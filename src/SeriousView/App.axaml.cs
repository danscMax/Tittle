using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
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
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IRecentFilesStore, RecentFilesStore>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
