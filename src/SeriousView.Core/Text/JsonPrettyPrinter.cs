using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace SeriousView.Core.Text;

/// <summary>Display-only JSON re-formatting for the source view (ported from the original
/// viewer's jsonPretty toggle). Pure: parse → indented serialize; anything unparseable
/// (broken JSON, JSONC with comments) returns null and the raw text is shown as-is — the
/// stored document text is never touched.</summary>
public static class JsonPrettyPrinter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Keep Cyrillic and friends readable instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public static string? TryFormat(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(doc.RootElement, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
