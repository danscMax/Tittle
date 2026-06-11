using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SeriousView.Core.Settings;

/// <summary>
/// System.Text.Json source-generation context for every type persisted through <c>ISettingsStore</c>:
/// <see cref="AppSettings"/> (key "settings") and the recent-files <see cref="List{String}"/> (key
/// "recent"). Wiring the store's options to <see cref="Default"/> uses generated metadata
/// (AOT-friendly, no per-type reflection) while the store's generic <c>Load&lt;T&gt;</c>/<c>Save&lt;T&gt;</c>
/// signatures stay unchanged. Enums are written as strings for readable, diff-able, migration-stable JSON
/// (numeric ordinals would silently remap an old file if a future enum value were inserted/reordered).
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(ViewStateFile))]
public sealed partial class AppJsonContext : JsonSerializerContext;
