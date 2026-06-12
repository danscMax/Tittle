using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

// ScaleTransform is an AvaloniaObject — constructing it needs the headless app runtime,
// so these run as Avalonia tests rather than plain [Fact]/[Theory].
public class ScaleTransformConverterTests
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(0.5)]
    public void Convert_BuildsUniformScaleTransform(double scale)
    {
        var result = ScaleTransformConverter.Instance.Convert(scale, typeof(Transform), null, Culture);

        var st = Assert.IsType<ScaleTransform>(result);
        Assert.Equal(scale, st.ScaleX);
        Assert.Equal(scale, st.ScaleY);
    }

    [AvaloniaTheory]
    [InlineData(0.0)]      // degenerate
    [InlineData(-1.0)]     // negative
    public void Convert_NonPositiveOrNonDouble_FallsBackToIdentity(double bad)
    {
        var fromBad = ScaleTransformConverter.Instance.Convert(bad, typeof(Transform), null, Culture);
        var fromNull = ScaleTransformConverter.Instance.Convert(null, typeof(Transform), null, Culture);

        Assert.Equal(1.0, Assert.IsType<ScaleTransform>(fromBad).ScaleX);
        Assert.Equal(1.0, Assert.IsType<ScaleTransform>(fromNull).ScaleX);
    }
}
