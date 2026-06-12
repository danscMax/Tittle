namespace SeriousView.Core.Text;

/// <summary>
/// RFC 4180-light CSV/TSV parser (ported from the original viewer's csv-as-table): quoted
/// fields may contain the delimiter, doubled quotes (<c>""</c> → <c>"</c>) and newlines.
/// The first record is the header; ragged rows are padded/cut to its width. Hard cap of
/// 10 000 data rows — <see cref="Truncated"/> tells the UI to say so. Null for empty input
/// (the caller falls back to the plain source view).
/// </summary>
public sealed class DelimitedTable
{
    public const int MaxRows = 10_000;

    public required string[] Header { get; init; }

    public required IReadOnlyList<string[]> Rows { get; init; }

    public bool Truncated { get; init; }

    public static DelimitedTable? Parse(string? text, char delimiter)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var records = SplitRecords(LineEndings.NormalizeToLf(text), delimiter);
        if (records.Count == 0)
            return null;

        var header = records[0];
        var width = header.Length;
        var truncated = records.Count - 1 > MaxRows;
        var rows = new List<string[]>(Math.Min(records.Count - 1, MaxRows));
        for (var i = 1; i < records.Count && rows.Count < MaxRows; i++)
        {
            var row = records[i];
            if (row.Length != width)
            {
                var fixedRow = new string[width];
                for (var c = 0; c < width; c++)
                    fixedRow[c] = c < row.Length ? row[c] : string.Empty;
                row = fixedRow;
            }

            rows.Add(row);
        }

        return new DelimitedTable { Header = header, Rows = rows, Truncated = truncated };
    }

    /// <summary>Single character walk honoring quotes; a record ends at an unquoted newline.</summary>
    private static List<string[]> SplitRecords(string text, char delimiter)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;
        // A quoted token (even an empty "") makes a single-field value EXPLICIT, so it must not be
        // confused with a bare blank line and dropped.
        var recordHasExplicitField = false;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField(); // always appends, so fields is never empty below
            // Drop a bare blank line (a single, empty, unquoted field), but keep a genuine record:
            // any multi-field row, a non-empty value, or an explicit quoted "" empty value.
            if (fields.Count > 1 || fields[0].Length > 0 || recordHasExplicitField)
                records.Add(fields.ToArray());
            fields.Clear();
            recordHasExplicitField = false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
                recordHasExplicitField = true;
            }
            else if (ch == delimiter)
            {
                EndField();
            }
            else if (ch == '\n')
            {
                EndRecord();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
            EndRecord();

        return records;
    }
}
