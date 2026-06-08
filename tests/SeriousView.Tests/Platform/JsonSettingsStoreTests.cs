using System;
using System.Collections.Generic;
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

    [Fact]
    public void Save_ThenLoad_RoundTripsLayout_ThroughSourceGen()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var settings = new AppSettings
            {
                Layout = new LayoutSettings
                {
                    MenuPlacement = MenuPlacement.TitleBar,
                    ToolbarMode = ToolbarMode.Fixed,
                    ViewTogglePlacement = ViewTogglePlacement.Omnibar,
                    ShowOmnibar = false,
                    ShowRail = true,
                },
            };

            store.Save("settings", settings);
            var loaded = store.Load<AppSettings>("settings")!;

            Assert.Equal(MenuPlacement.TitleBar, loaded.Layout!.MenuPlacement);
            Assert.Equal(ToolbarMode.Fixed, loaded.Layout.ToolbarMode);
            Assert.Equal(ViewTogglePlacement.Omnibar, loaded.Layout.ViewTogglePlacement);
            Assert.False(loaded.Layout.ShowOmnibar);
            Assert.True(loaded.Layout.ShowRail);
            Assert.Equal(AppSettingsMigrator.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_WritesEnumsAsStrings_NotNumbers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            store.Save("settings", new AppSettings
            {
                Layout = new LayoutSettings { MenuPlacement = MenuPlacement.TitleBar, ToolbarMode = ToolbarMode.Fixed },
            });

            var json = File.ReadAllText(Path.Combine(dir, "settings.json"));

            Assert.Contains("\"TitleBar\"", json); // string enum → source-gen string converter is wired
            Assert.Contains("\"Fixed\"", json);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_RealLegacyJson_NumericEnums_NoSchemaOrLayout_ReadsWithDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            // The actual pre-M7.5 file format: numeric enums (reflection serializer), no schemaVersion, no layout.
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                "{\"Theme\":0,\"Window\":{\"Width\":800,\"Height\":600,\"X\":10,\"Y\":20,\"Maximized\":false}," +
                "\"Editor\":{\"FontSize\":14,\"WordWrap\":false,\"ShowLineNumbers\":true}}");

            var loaded = new JsonSettingsStore(dir).Load<AppSettings>("settings")!;

            Assert.Equal(ThemeMode.Dark, loaded.Theme); // numeric 0 still reads (StringEnumConverter accepts ints)
            Assert.Equal(800, loaded.Window!.Width);
            Assert.Equal(14, loaded.Editor!.FontSize);
            Assert.Null(loaded.Layout);             // absent → null → defaults at the consumer
            Assert.Equal(0, loaded.SchemaVersion);  // store doesn't migrate; that's the service's job
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsRecentList_ThroughSourceGen()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sv_settings_" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(dir);
            var recent = new List<string> { @"C:\a.md", @"C:\b.md" };

            store.Save("recent", recent);
            var loaded = store.Load<List<string>>("recent");

            Assert.Equal(recent, loaded); // List<string> must be registered in AppJsonContext
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
