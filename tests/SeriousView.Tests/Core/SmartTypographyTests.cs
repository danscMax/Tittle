using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class SmartTypographyTests
{
    [Theory]
    [InlineData("один -- два", "один — два")]
    [InlineData("шаг -> результат", "шаг → результат")]
    [InlineData("вход => выход", "вход ⇒ выход")]
    [InlineData("подумать...", "подумать…")]
    [InlineData("он сказал \"привет\" и ушёл", "он сказал «привет» и ушёл")]
    public void Apply_ProseReplacements(string input, string expected)
        => Assert.Equal(expected, SmartTypography.Apply(input));

    [Theory]
    [InlineData("if (a == b) { return x; }")]
    [InlineData("const f = () => \"raw\";")]
    [InlineData("a && b || c")]
    public void Apply_CodeLikeLines_AreLeftAlone(string line)
        => Assert.Equal(line, SmartTypography.Apply(line));

    [Fact]
    public void Apply_MixedLines_OnlyProseChanges()
    {
        var result = SmartTypography.Apply("проза -- тут\nvar x = \"code\";");

        Assert.Equal("проза — тут\nvar x = \"code\";", result);
    }

    [Fact]
    public void Apply_KeepsLineCount()
    {
        var result = SmartTypography.Apply("a...\nb\nc -- d");

        Assert.Equal(3, result.Split('\n').Length);
    }
}
