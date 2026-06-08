using System.Globalization;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

public class EnumToBoolConverterTests
{
    private static object? Convert(object? value, string? parameter)
        => EnumToBoolConverter.Instance.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

    [Fact]
    public void Matches_SameEnumMember()
        => Assert.True((bool)Convert(MenuPlacement.Hidden, "Hidden")!);

    [Fact]
    public void DoesNotMatch_DifferentMember()
        => Assert.False((bool)Convert(MenuPlacement.Hidden, "Bar")!);

    [Fact]
    public void Null_Value_IsFalse()
        => Assert.False((bool)Convert(null, "Hidden")!);

    [Fact]
    public void Works_AcrossEnumTypes()
    {
        Assert.True((bool)Convert(ThemeMode.Light, "Light")!);
        Assert.False((bool)Convert(ThemeMode.Light, "Dark")!);
    }
}
