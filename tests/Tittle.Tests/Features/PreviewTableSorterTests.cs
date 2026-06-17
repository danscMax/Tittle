using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

public class PreviewTableSorterTests
{
    private const string TableMd = "| n | name |\n|---|---|\n| 2 | x |\n| 10 | y |\n| 1 | z |";

    private static (Window Window, Grid Table) RenderTable()
    {
        var vm = DocumentTabViewModel.FromFile(TableMd, "/docs/t.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var preview = view.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("Preview")!;
        var table = preview.GetVisualDescendants().OfType<Grid>()
            .First(g => g.Classes.Contains("Table"));
        PreviewTableSorter.AttachAll(preview);
        return (window, table);
    }

    private static string CellAt(Grid table, int row, int col)
    {
        var border = table.Children.OfType<Border>()
            .First(b => Grid.GetRow(b) == row && Grid.GetColumn(b) == col);
        return border.GetVisualDescendants()
            .OfType<ColorTextBlock.Avalonia.CTextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? string.Empty;
    }

    [AvaloniaFact]
    public void Sort_NumericColumn_OrdersByValueNotText()
    {
        var (window, table) = RenderTable();
        Assert.Contains("sortable-attached", table.Classes);

        PreviewTableSorter.Sort(table, 0);

        // Numeric: 1, 2, 10 — a textual sort would give 1, 10, 2.
        Assert.Equal("1", CellAt(table, 1, 0));
        Assert.Equal("2", CellAt(table, 2, 0));
        Assert.Equal("10", CellAt(table, 3, 0));
        Assert.Equal("z", CellAt(table, 1, 1)); // the row moved as a unit
        window.Close();
    }

    [AvaloniaFact]
    public void Sort_SecondClick_Reverses()
    {
        var (window, table) = RenderTable();

        PreviewTableSorter.Sort(table, 0);
        PreviewTableSorter.Sort(table, 0);

        Assert.Equal("10", CellAt(table, 1, 0));
        Assert.Equal("1", CellAt(table, 3, 0));
        window.Close();
    }

    [AvaloniaFact]
    public void Sort_RedealsTheZebraClasses()
    {
        var (window, table) = RenderTable();

        PreviewTableSorter.Sort(table, 0);

        var row1 = table.Children.OfType<Border>().First(b => Grid.GetRow(b) == 1 && Grid.GetColumn(b) == 0);
        var row3 = table.Children.OfType<Border>().First(b => Grid.GetRow(b) == 3 && Grid.GetColumn(b) == 0);
        Assert.Contains("OddTableRow", row1.Classes);
        Assert.Contains("FirstTableRow", row1.Classes);
        Assert.Contains("LastTableRow", row3.Classes);
        Assert.DoesNotContain("FirstTableRow", row3.Classes);
        window.Close();
    }
}
