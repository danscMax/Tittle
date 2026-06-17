using Tittle.Core.Settings;

namespace Tittle.Core.Abstractions;

/// <summary>
/// In-memory holder for the single <see cref="AppSettings"/> record: loaded once at startup and
/// persisted atomically via <see cref="ISettingsStore"/>. One shared instance means independent
/// writers (theme, window, session) never clobber each other's fields.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>The current settings (never null).</summary>
    AppSettings Current { get; }

    /// <summary>Replace the settings and persist them. Use <c>Current with { ... }</c> to change one field.</summary>
    void Update(AppSettings next);
}
