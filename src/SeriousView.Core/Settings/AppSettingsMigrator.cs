namespace SeriousView.Core.Settings;

/// <summary>
/// Upgrades a loaded <see cref="AppSettings"/> from an older on-disk schema to the current one, so
/// growing the settings over milestones never silently drops data. Pure and typed — it lives here
/// (not in the generic <c>ISettingsStore</c>, which doesn't know the type) and is called by
/// <c>AppSettingsService</c>. v1 is the baseline; legacy files predate <c>schemaVersion</c>.
/// </summary>
public static class AppSettingsMigrator
{
    /// <summary>The schema version this build writes. Bump when an in-place migration is added below.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Normalize a just-loaded settings object to <see cref="CurrentSchemaVersion"/>. Null in → null
    /// out (the caller substitutes defaults). A legacy file has <c>SchemaVersion == 0</c> (the field
    /// was absent), treated as the v1 baseline: no fields are dropped, missing sections stay null →
    /// defaults.
    /// </summary>
    public static AppSettings? Migrate(AppSettings? loaded)
    {
        if (loaded is null)
            return null;

        var settings = loaded;

        // v0 (pre-versioned) → v1: stamp the version. New optional fields (e.g. Layout) default to
        // null, so nothing is lost; older readers ignored unknown fields and newer readers fill defaults.
        if (settings.SchemaVersion < 1)
            settings = settings with { SchemaVersion = 1 };

        // Future migrations chain here, e.g.:
        // if (settings.SchemaVersion < 2) settings = MigrateV1ToV2(settings);

        // A file written by a newer build than this one: clamp the stamp down so we don't claim a
        // version we can't honor. Data is left untouched (unknown fields are simply ignored on read).
        if (settings.SchemaVersion > CurrentSchemaVersion)
            settings = settings with { SchemaVersion = CurrentSchemaVersion };

        return settings;
    }
}
