using System.Text.Json;
using SeriousView.Core.Settings;

namespace SeriousView.Features.Shell;

/// <summary>
/// Settings import/export serialization (extracted from <see cref="MainWindowViewModel"/>): turns the
/// shareable preferences into JSON and parses+validates an imported file. Whitelisting comes by
/// construction — the typed <see cref="AppSettings"/> record ignores unknown keys. Applying a parsed
/// result to the live editor/layout options stays in the VM (it owns those observables); this type
/// only owns the file format and the "is this actually a settings file?" guard.
/// </summary>
public static class SettingsTransfer
{
    private static readonly JsonSerializerOptions Json =
        new() { TypeInfoResolver = AppJsonContext.Default, WriteIndented = true };

    public enum ParseStatus
    {
        Ok,
        NotSettings, // empty `{}` or an unrelated file — would silently reset preferences
        Invalid,     // malformed JSON / null
    }

    /// <summary>Serialize the PREFERENCES only — session (absolute open-file paths) and window
    /// placement are machine-private state, never shareable.</summary>
    public static string Serialize(AppSettings current)
        => JsonSerializer.Serialize(current with { Session = null, Window = null }, Json);

    /// <summary>Parse + validate an imported settings file. An empty `{}` (or any non-settings JSON)
    /// would deserialize to an all-default <see cref="AppSettings"/> and silently reset everything, so
    /// it is rejected as <see cref="ParseStatus.NotSettings"/> before any value is returned.</summary>
    public static (ParseStatus Status, AppSettings? Settings) Parse(string raw)
    {
        if (!LooksLikeSettings(raw))
            return (ParseStatus.NotSettings, null);

        try
        {
            var parsed = JsonSerializer.Deserialize<AppSettings>(raw, Json);
            return parsed is null ? (ParseStatus.Invalid, null) : (ParseStatus.Ok, parsed);
        }
        catch (JsonException)
        {
            return (ParseStatus.Invalid, null);
        }
    }

    /// <summary>True only when the JSON is an object carrying at least one recognised settings key.</summary>
    private static bool LooksLikeSettings(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "Theme", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, "Editor", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, "Layout", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
