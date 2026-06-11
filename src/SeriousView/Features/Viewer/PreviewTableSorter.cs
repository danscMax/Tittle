using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

/// <summary>Click-to-sort for rendered preview tables (ported `setupTables`).
/// Markdown.Avalonia renders a GFM table as a <c>Grid.Table</c> of <c>Border</c> cells
/// (<c>TableHeader</c> on row 0, zebra classes on the body) — a header click reorders the
/// body cells' <c>Grid.Row</c> in place and re-deals the zebra. Numeric-vs-ordinal column
/// sniffing is shared with the CSV view (<see cref="TableSorting"/>).</summary>
public static class PreviewTableSorter
{
    private const string AttachedClass = "sortable-attached";

    private sealed record SortState(int Column, bool Descending);

    /// <summary>Wires every not-yet-wired table under <paramref name="root"/> (idempotent;
    /// called from the preview's reflow pass, like the code-block fixups).</summary>
    public static void AttachAll(Visual root)
    {
        foreach (var table in root.GetVisualDescendants().OfType<Grid>()
                     .Where(g => g.Classes.Contains("Table") && !g.Classes.Contains(AttachedClass))
                     .ToList())
        {
            table.Classes.Add(AttachedClass);
            foreach (var header in table.Children.OfType<Border>()
                         .Where(b => b.Classes.Contains("TableHeader")))
            {
                var column = Grid.GetColumn(header);
                header.Cursor = new Cursor(StandardCursorType.Hand);
                ToolTip.SetTip(header, "Сортировать по столбцу");
                header.PointerPressed += (_, e) =>
                {
                    Sort(table, column);
                    e.Handled = true;
                };
            }
        }
    }

    /// <summary>Sorts the table body by <paramref name="column"/>; a repeat click on the
    /// same column reverses. Internal so headless tests can drive it directly.</summary>
    internal static void Sort(Grid table, int column)
    {
        var rows = table.Children.OfType<Border>()
            .Where(b => !b.Classes.Contains("TableHeader"))
            .GroupBy(Grid.GetRow)
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();
        if (rows.Count == 0)
            return;

        var descending = table.Tag is SortState s && s.Column == column && !s.Descending;
        table.Tag = new SortState(column, descending);

        string KeyOf(System.Collections.Generic.List<Border> row)
            => row.FirstOrDefault(c => Grid.GetColumn(c) == column) is { } cell
                ? CellText(cell)
                : string.Empty;

        var ordered = TableSorting.IsNumericColumn(rows.Select(KeyOf))
            ? rows.OrderBy(r => TableSorting.NumericKey(KeyOf(r)))
            : rows.OrderBy(KeyOf, System.StringComparer.CurrentCultureIgnoreCase);
        var sorted = (descending ? ordered.Reverse() : ordered).ToList();

        for (var i = 0; i < sorted.Count; i++)
        {
            var rowIndex = i + 1; // the header keeps row 0
            foreach (var cell in sorted[i])
            {
                Grid.SetRow(cell, rowIndex);
                // The zebra/edge classes are positional — re-deal them for the new order.
                cell.Classes.Remove("OddTableRow");
                cell.Classes.Remove("EvenTableRow");
                cell.Classes.Remove("FirstTableRow");
                cell.Classes.Remove("LastTableRow");
                cell.Classes.Add(rowIndex % 2 == 1 ? "OddTableRow" : "EvenTableRow");
                if (i == 0)
                    cell.Classes.Add("FirstTableRow");
                if (i == sorted.Count - 1)
                    cell.Classes.Add("LastTableRow");
            }
        }
    }

    private static string CellText(Visual cell)
    {
        foreach (var visual in cell.GetVisualDescendants())
        {
            switch (visual)
            {
                case ColorTextBlock.Avalonia.CTextBlock c when !string.IsNullOrEmpty(c.Text):
                    return c.Text;
                case TextBlock t when !string.IsNullOrEmpty(t.Text):
                    return t.Text;
            }
        }

        return string.Empty;
    }
}
