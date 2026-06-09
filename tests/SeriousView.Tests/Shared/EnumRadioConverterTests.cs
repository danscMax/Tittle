using System.Globalization;
using Avalonia.Data;
using SeriousView.Core.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

public class EnumRadioConverterTests
{
    private static object? Convert(object? value, string param)
        => EnumRadioConverter.Instance.Convert(value, typeof(bool), param, CultureInfo.InvariantCulture);

    private static object? ConvertBack(object? value, string param)
        => EnumRadioConverter.Instance.ConvertBack(value, typeof(ToolbarMode), param, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_True_WhenEnumMatchesParameter()
        => Assert.True((bool)Convert(ToolbarMode.Contextual, "Contextual")!);

    [Fact]
    public void Convert_False_WhenDiffers()
        => Assert.False((bool)Convert(ToolbarMode.Off, "Contextual")!);

    [Fact]
    public void ConvertBack_Checked_ReturnsTheEnumMember()
        => Assert.Equal(ToolbarMode.Fixed, ConvertBack(true, "Fixed"));

    [Fact]
    public void ConvertBack_Unchecked_DoesNothing()
        => Assert.Equal(BindingOperations.DoNothing, ConvertBack(false, "Fixed"));
}
