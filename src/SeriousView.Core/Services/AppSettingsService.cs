using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;

namespace SeriousView.Core.Services;

/// <summary>
/// Default <see cref="IAppSettingsService"/>: loads <see cref="AppSettings"/> from the
/// <see cref="ISettingsStore"/> on construction and writes it back on every <see cref="Update"/>.
/// </summary>
public sealed class AppSettingsService : IAppSettingsService
{
    private const string Key = "settings";

    private readonly ISettingsStore _store;

    public AppSettingsService(ISettingsStore store)
    {
        _store = store;
        // Migrate normalizes an older on-disk schema (stamps version, keeps data) before we hold it.
        Current = AppSettingsMigrator.Migrate(store.Load<AppSettings>(Key)) ?? new AppSettings();
    }

    public AppSettings Current { get; private set; }

    public void Update(AppSettings next)
    {
        Current = next;
        _store.Save(Key, next);
    }
}
