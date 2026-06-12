using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SeriousView.Core.Text;

/// <summary>Shared click-to-sort semantics for tabular views (CSV table + preview tables):
/// a column sorts numerically when every non-empty sampled value parses as a number,
/// ordinally otherwise; unparsable values sink to the bottom.</summary>
public static class TableSorting
{
    /// <summary>Sample size for the numeric-column sniff (matches the CSV view).</summary>
    public const int NumericSample = 200;

    public static bool IsNumericColumn(IEnumerable<string> values)
    {
        var sampled = values.Take(NumericSample).Where(v => v.Length > 0).ToList();
        return sampled.Count > 0 && sampled.All(v =>
            double.TryParse(Normalize(v), NumberStyles.Any, CultureInfo.InvariantCulture, out _));
    }

    /// <summary>Sort key for a numeric column. Q19: a (Unparsed, Value) tuple keeps garbage in its
    /// own partition so a real <c>double.MaxValue</c> cell no longer collides with the "sort last"
    /// sentinel — ascending puts parsed values first (by magnitude), unparsable cells last.</summary>
    public static (bool Unparsed, double Value) NumericKey(string value)
        => double.TryParse(Normalize(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? (false, d)
            : (true, 0d);

    // Russian-styled numbers ("1 000", "1,5", NBSP groups) must sort numerically too —
    // the target content is ru, and the cv-* layer renders exactly this convention.
    private static string Normalize(string value)
        => value.Trim().Replace(" ", "").Replace(" ", "").Replace(',', '.');
}
