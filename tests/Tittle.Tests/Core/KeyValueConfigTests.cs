using System.Linq;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class KeyValueConfigTests
{
    [Fact]
    public void Parse_Ini_ReadsKeyValuePairs()
    {
        var table = KeyValueConfig.Parse("name = Tittle\nversion = 1");

        Assert.NotNull(table);
        Assert.Equal(["Ключ", "Значение"], table!.Header);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["name", "Tittle"], table.Rows[0]);
        Assert.Equal(["version", "1"], table.Rows[1]);
    }

    [Fact]
    public void Parse_TomlSections_PrefixKeysWithSection()
    {
        var table = KeyValueConfig.Parse("[server]\nhost = \"localhost\"\nport = 8080");

        Assert.NotNull(table);
        Assert.Equal(["server.host", "localhost"], table!.Rows[0]); // section prefix + unquoted
        Assert.Equal(["server.port", "8080"], table.Rows[1]);
    }

    [Fact]
    public void Parse_DotEnv_StripsExportAndComments()
    {
        var table = KeyValueConfig.Parse("# a comment\nexport TOKEN=abc # inline\n; semicolon comment\nEMPTY=");

        Assert.NotNull(table);
        Assert.Equal(2, table!.Rows.Count);
        Assert.Equal(["TOKEN", "abc"], table.Rows[0]); // export stripped, inline comment dropped
        Assert.Equal(["EMPTY", ""], table.Rows[1]);
    }

    [Fact]
    public void Parse_ValueWithEquals_SplitsOnFirstOnly()
    {
        var table = KeyValueConfig.Parse("url = https://x/?a=1&b=2");
        Assert.Equal(["url", "https://x/?a=1&b=2"], table!.Rows.Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# only comments\n; nothing else")]
    [InlineData("just prose, no equals sign")]
    public void Parse_NoPairs_ReturnsNull(string input)
        => Assert.Null(KeyValueConfig.Parse(input));
}
