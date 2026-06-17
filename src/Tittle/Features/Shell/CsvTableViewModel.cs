using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tittle.Core.Text;

namespace Tittle.Features.Shell;

/// <summary>
/// Presentation model for the CSV/TSV table view (ported from the original viewer's
/// csv-as-table): pre-measured column widths, click-to-sort headers (numeric when the
/// column parses as numbers, ordinal otherwise; second click reverses), 10k-row cap notice.
/// </summary>
public sealed partial class CsvTableViewModel : ObservableObject
{
    public sealed record Cell(string Text, double Width);

    public sealed record Row(IReadOnlyList<Cell> Cells);

    public sealed record Column(string Header, double Width, int Index);

    private const double MinWidth = 60;
    private const double MaxWidth = 420;
    private const double PxPerChar = 8.2;

    public IReadOnlyList<Column> Columns { get; }

    public bool Truncated { get; }

    [ObservableProperty]
    private IReadOnlyList<Row> _rows;

    /// <summary>Index of the sorted column, −1 = file order.</summary>
    [ObservableProperty]
    private int _sortColumn = -1;

    [ObservableProperty]
    private bool _sortDescending;

    private readonly IReadOnlyList<string[]> _source;

    public CsvTableViewModel(DelimitedTable table)
    {
        _source = table.Rows;
        Truncated = table.Truncated;

        // Width per column from the longest text among the header and a sample of rows.
        var widths = new double[table.Header.Length];
        for (var c = 0; c < table.Header.Length; c++)
        {
            var longest = table.Header[c].Length;
            foreach (var row in table.Rows.Take(200))
                longest = Math.Max(longest, row[c].Length);
            widths[c] = Math.Clamp(longest * PxPerChar + 24, MinWidth, MaxWidth);
        }

        Columns = table.Header.Select((h, i) => new Column(h, widths[i], i)).ToList();
        _rows = Materialize(table.Rows, widths);
    }

    [RelayCommand]
    private void SortBy(Column? column)
    {
        if (column is null)
            return;

        SortDescending = column.Index == SortColumn && !SortDescending;
        SortColumn = column.Index;

        var numeric = TableSorting.IsNumericColumn(_source.Select(r => r[column.Index]));

        // LINQ OrderBy/OrderByDescending are stable, so flipping direction with OrderByDescending
        // (rather than OrderBy().Reverse()) preserves the relative order of equal-key rows instead
        // of reshuffling ties on every descending click.
        IEnumerable<string[]> sorted = (numeric, SortDescending) switch
        {
            (true, false) => _source.OrderBy(r => TableSorting.NumericKey(r[column.Index])),
            (true, true) => _source.OrderByDescending(r => TableSorting.NumericKey(r[column.Index])),
            (false, false) => _source.OrderBy(r => r[column.Index], StringComparer.CurrentCultureIgnoreCase),
            (false, true) => _source.OrderByDescending(r => r[column.Index], StringComparer.CurrentCultureIgnoreCase),
        };

        Rows = Materialize(sorted.ToList(), Columns.Select(c => c.Width).ToArray());
    }

    private static IReadOnlyList<Row> Materialize(IReadOnlyList<string[]> rows, IReadOnlyList<double> widths)
        => rows.Select(r => new Row(r.Select((v, i) => new Cell(v, widths[i])).ToList())).ToList();
}
