using System;
using System.IO;
using System.Text.Json;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;

namespace SeriousView.Platform;

/// <summary>
/// <see cref="ISettingsStore"/> backed by JSON files under the per-user app-data folder
/// (Roaming on Windows, ~/.config on Linux, ~/Library/Application Support on macOS).
/// Read/parse failures degrade to default instead of throwing.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    // Source-generated metadata (AOT-friendly, no per-type reflection) for every persisted type
    // registered in AppJsonContext (AppSettings + the recent-files List<string>). The generic
    // Load/Save signatures stay unchanged — the resolver supplies metadata for the concrete T.
    private static readonly JsonSerializerOptions Options = new() { TypeInfoResolver = AppJsonContext.Default };

    private readonly string _dir;

    /// <param name="directory">Storage folder; defaults to <c>%AppData%/SeriousView</c>.
    /// Overridable so tests can point at a temp directory.</param>
    public JsonSettingsStore(string? directory = null)
    {
        _dir = directory ?? AppPaths.DataDir;
        Directory.CreateDirectory(_dir);
    }

    public T? Load<T>(string key)
    {
        var file = Path.Combine(_dir, key + ".json");
        if (!File.Exists(file))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file), Options);
        }
        catch
        {
            return default; // missing/corrupt → start fresh
        }
    }

    public void Save<T>(string key, T value)
    {
        var file = Path.Combine(_dir, key + ".json");
        var temp = file + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            // Atomic swap: temp and target share the directory/filesystem, so this is atomic
            // (Windows ReplaceFile, Unix rename) — a crash mid-write can't corrupt the live file.
            if (File.Exists(file))
                File.Replace(temp, file, null);
            else
                File.Move(temp, file);
        }
        catch
        {
            // Best-effort persistence; drop a stray temp file and ignore I/O errors.
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch
            {
                // ignore
            }
        }
    }
}
