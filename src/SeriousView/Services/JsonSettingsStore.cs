using System;
using System.IO;
using System.Text.Json;
using SeriousView.Core.Abstractions;

namespace SeriousView.Services;

/// <summary>
/// <see cref="ISettingsStore"/> backed by JSON files under the per-user app-data folder
/// (Roaming on Windows, ~/.config on Linux, ~/Library/Application Support on macOS).
/// Read/parse failures degrade to default instead of throwing.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _dir;

    public JsonSettingsStore()
    {
        _dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SeriousView");
        Directory.CreateDirectory(_dir);
    }

    public T? Load<T>(string key)
    {
        var file = Path.Combine(_dir, key + ".json");
        if (!File.Exists(file))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file));
        }
        catch
        {
            return default; // missing/corrupt → start fresh
        }
    }

    public void Save<T>(string key, T value)
    {
        try
        {
            File.WriteAllText(Path.Combine(_dir, key + ".json"), JsonSerializer.Serialize(value));
        }
        catch
        {
            // Best-effort persistence; ignore I/O errors.
        }
    }
}
