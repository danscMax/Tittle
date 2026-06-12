using System.Globalization;
using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class TableSortingTests
{
    [Fact]
    public void NumericKey_RealMaxValue_SortsBeforeGarbage()
    {
        // Q19: a genuine double.MaxValue cell must not collide with the "sort last" sentinel —
        // ascending puts parsed values (by magnitude) first and unparsable cells last.
        var max = double.MaxValue.ToString("R", CultureInfo.InvariantCulture);
        var cells = new[] { "junk", max, "42" };

        var ascending = cells.OrderBy(TableSorting.NumericKey).ToList();

        Assert.Equal(new[] { "42", max, "junk" }, ascending);
    }

    [Fact]
    public void NumericKey_ParsesRussianStyledNumbers()
    {
        Assert.False(TableSorting.NumericKey("1 000,5").Unparsed); // NBSP groups + comma decimal
        Assert.Equal(1000.5, TableSorting.NumericKey("1 000,5").Value);
        Assert.True(TableSorting.NumericKey("n/a").Unparsed);
    }
}
