using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SeriousView.ViewModels;
using SeriousView.Views;

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

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
