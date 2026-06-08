using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;
using Xunit;

namespace SeriousView.Tests.Core;

public class AppSettingsMigratorTests
{
    [Fact]
    public void Migrate_Null_ReturnsNull()
        => Assert.Null(AppSettingsMigrator.Migrate(null));

    [Fact]
    public void Migrate_LegacyV0_StampsV1_AndPreservesData()
    {
        var legacy = new AppSettings { SchemaVersion = 0, Theme = ThemeMode.Light };

        var migrated = AppSettingsMigrator.Migrate(legacy)!;

        Assert.Equal(1, migrated.SchemaVersion);
        Assert.Equal(ThemeMode.Light, migrated.Theme); // nothing dropped
    }

    [Fact]
    public void Migrate_AlreadyCurrent_IsUnchanged()
    {
        var current = new AppSettings { Layout = new LayoutSettings { ShowRail = true } };

        var migrated = AppSettingsMigrator.Migrate(current)!;

        Assert.Equal(AppSettingsMigrator.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.True(migrated.Layout!.ShowRail);
    }

    [Fact]
    public void Migrate_FutureVersion_ClampsDown_WithoutLosingData()
    {
        var future = new AppSettings { SchemaVersion = 999, Theme = ThemeMode.Auto };

        var migrated = AppSettingsMigrator.Migrate(future)!;

        Assert.Equal(AppSettingsMigrator.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(ThemeMode.Auto, migrated.Theme);
    }
}
