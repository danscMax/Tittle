using Tittle.Core.Abstractions;
using Tittle.Core.Settings;
using Xunit;

namespace Tittle.Tests.Core;

public class AppSettingsMigratorTests
{
    [Fact]
    public void Migrate_Null_ReturnsNull()
        => Assert.Null(AppSettingsMigrator.Migrate(null));

    [Fact]
    public void Migrate_LegacyV0_StampsToCurrent_AndPreservesData()
    {
        var legacy = new AppSettings { SchemaVersion = 0, Theme = ThemeMode.Light };

        var migrated = AppSettingsMigrator.Migrate(legacy)!;

        Assert.Equal(AppSettingsMigrator.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(ThemeMode.Light, migrated.Theme); // nothing dropped
    }

    [Fact]
    public void Migrate_V1ToV2_StampsV2_AndPreservesData_WithSplitDefaults()
    {
        // v1→v2 added LayoutSettings.SplitOrientation/SplitRatio — a pure stamp bump. An old Layout
        // section keeps its data and the new fields default to Horizontal/0.5.
        var v1 = new AppSettings { SchemaVersion = 1, Layout = new LayoutSettings { ShowRail = true } };

        var migrated = AppSettingsMigrator.Migrate(v1)!;

        Assert.Equal(2, migrated.SchemaVersion);
        Assert.True(migrated.Layout!.ShowRail); // nothing dropped
        Assert.Equal(SplitOrientation.Horizontal, migrated.Layout.SplitOrientation);
        Assert.Equal(0.5, migrated.Layout.SplitRatio);
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
