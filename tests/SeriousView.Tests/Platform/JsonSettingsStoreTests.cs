using System;
using System.IO;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;
using SeriousView.Platform;
using Xunit;

namespace SeriousView.Tests.Platform;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Save_ThenLoad_RoundTripsAppSettings_LeavingNoTempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var settings = new AppSettings
            {
                Theme = ThemeMode.Light,
                Window = new WindowPlacement(800, 600, 10, 20, Maximized: true),
            };

            store.Save("settings", settings);
            var loaded = store.Load<AppSettings>("settings");

            Assert.NotNull(loaded);
            Assert.Equal(ThemeMode.Light, loaded!.Theme);
            Assert.Equal(800, loaded.Window!.Width);
            Assert.Equal(20, loaded.Window.Y);
            Assert.True(loaded.Window.Maximized);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp")); // atomic swap left no temp behind
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_OverwritesExistingFile_Atomically()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);

            store.Save("settings", new AppSettings { Theme = ThemeMode.Dark });
            store.Save("settings", new AppSettings { Theme = ThemeMode.Auto }); // replace existing

            Assert.Equal(ThemeMode.Auto, store.Load<AppSettings>("settings")!.Theme);
            Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
