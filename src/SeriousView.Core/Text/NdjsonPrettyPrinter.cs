using System.Text;

namespace SeriousView.Core.Text;

/// <summary>Display-only pretty-print for newline-delimited JSON (<c>.ndjson</c>/<c>.jsonl</c>):
/// each non-empty line is an independent JSON value. Best-effort — every parseable line is
/// indented (via <see cref="JsonPrettyPrinter"/>) and the formatted records are separated by a
/// blank line; a line that doesn't parse is kept verbatim. Returns null only when nothing parsed
/// (so the raw source is shown as-is). The stored document text is never touched.</summary>
public static class NdjsonPrettyPrinter
{
    public static string? TryFormat(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var lines = LineEndings.NormalizeToLf(text).Split('\n');
        var sb = new StringBuilder();
        var anyFormatted = false;
        var first = true;

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
                continue;

            if (!first)
                sb.Append("\n\n");
            first = false;

            if (JsonPrettyPrinter.TryFormat(line) is { } formatted)
            {
                sb.Append(formatted);
                anyFormatted = true;
            }
            else
            {
                sb.Append(line);
            }
        }

        return anyFormatted ? sb.ToString() : null;
    }
}
