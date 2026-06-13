using System.Collections.Generic;

namespace SeriousView.Core.Text;

/// <summary>Pure parser for key/value configuration files (INI, <c>.env</c>, TOML) into the
/// shared <see cref="DelimitedTable"/> shape (header <c>Ключ | Значение</c>), so they render in
/// the existing sortable table overlay — the standalone-file counterpart of the markdown
/// front-matter «Метаданные» panel. Section headers (<c>[section]</c>) prefix their keys
/// (<c>section.key</c>); <c>#</c>/<c>;</c> comments and blank lines are skipped; surrounding
/// quotes and a leading <c>export</c> (.env) are stripped from values. Returns null when no
/// key/value pair is found, so the caller falls back to the plain source view.</summary>
public static class KeyValueConfig
{
    private static readonly string[] Header = ["Ключ", "Значение"];

    public static DelimitedTable? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var rows = new List<string[]>();
        var section = "";

        foreach (var raw in LineEndings.NormalizeToLf(text).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';')
                continue;

            // Section header: [section] or TOML array-of-tables [[section]].
            if (line[0] == '[' && line[^1] == ']')
            {
                section = line.Trim('[', ']').Trim();
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue; // not a key=value line (e.g. a TOML array continuation) — skip

            var key = line[..eq].Trim();
            if (key.StartsWith("export ", System.StringComparison.Ordinal))
                key = key["export ".Length..].Trim();
            if (key.Length == 0)
                continue;

            var value = StripInlineComment(line[(eq + 1)..].Trim());
            value = Unquote(value);

            var fullKey = section.Length > 0 ? $"{section}.{key}" : key;
            rows.Add([fullKey, value]);
        }

        return rows.Count == 0 ? null : new DelimitedTable { Header = Header, Rows = rows };
    }

    // Drop a trailing ` # ...` / ` ; ...` inline comment from an UNQUOTED value. A value that is
    // quoted is returned untouched (the # may be part of it) and unquoted by the caller next.
    private static string StripInlineComment(string value)
    {
        if (value.Length > 0 && value[0] is '"' or '\'')
            return value;

        for (var i = 1; i < value.Length; i++)
            if (value[i] is '#' or ';' && value[i - 1] == ' ')
                return value[..i].TrimEnd();
        return value;
    }

    private static string Unquote(string value)
        => value.Length >= 2 && (value[0] == '"' && value[^1] == '"' || value[0] == '\'' && value[^1] == '\'')
            ? value[1..^1]
            : value;
}
