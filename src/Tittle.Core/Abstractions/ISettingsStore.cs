namespace Tittle.Core.Abstractions;

/// <summary>Persists small typed settings by key. Implementation (JSON/AppData) is UI-side.</summary>
public interface ISettingsStore
{
    T? Load<T>(string key);

    void Save<T>(string key, T value);
}
