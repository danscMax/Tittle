using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class DelimitedTableTests
{
    [Fact]
    public void Parse_SimpleCsv_HeaderAndRows()
    {
        var table = DelimitedTable.Parse("name,age\nАня,30\nБорис,25", ',');

        Assert.NotNull(table);
        Assert.Equal(new[] { "name", "age" }, table!.Header);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(new[] { "Аня", "30" }, table.Rows[0]);
        Assert.False(table.Truncated);
    }

    [Fact]
    public void Parse_QuotedFields_WithDelimitersEscapesAndNewlines()
    {
        var table = DelimitedTable.Parse("a,b\n\"x,y\",\"he said \"\"hi\"\"\"\n\"multi\nline\",2", ',');

        Assert.Equal(new[] { "x,y", "he said \"hi\"" }, table!.Rows[0]);
        Assert.Equal("multi\nline", table.Rows[1][0]);
    }

    [Fact]
    public void Parse_Tsv_UsesTabs()
    {
        var table = DelimitedTable.Parse("a\tb\n1\t2", '\t');

        Assert.Equal(new[] { "1", "2" }, table!.Rows[0]);
    }

    [Fact]
    public void Parse_RaggedRows_ArePaddedToTheHeader()
    {
        var table = DelimitedTable.Parse("a,b,c\n1,2\n1,2,3,4", ',');

        Assert.All(table!.Rows, r => Assert.Equal(3, r.Length));
        Assert.Equal("", table.Rows[0][2]);
    }

    [Fact]
    public void Parse_CapsAt10kRows_AndFlagsTruncation()
    {
        var text = "h\n" + string.Join("\n", Enumerable.Range(1, 10_500).Select(i => i.ToString()));

        var table = DelimitedTable.Parse(text, ',');

        Assert.Equal(10_000, table!.Rows.Count);
        Assert.True(table.Truncated);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Parse_EmptyInput_ReturnsNull(string input)
        => Assert.Null(DelimitedTable.Parse(input, ','));
}
