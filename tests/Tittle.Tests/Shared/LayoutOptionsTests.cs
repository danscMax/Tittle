using Tittle.Core.Settings;
using Tittle.Shared;
using Xunit;

namespace Tittle.Tests.Shared;

public class LayoutOptionsTests
{
    [Fact]
    public void FromSettings_Null_GivesEtalonDefaults()
    {
        var o = LayoutOptions.FromSettings(null);

        Assert.Equal(MenuPlacement.Hidden, o.MenuPlacement);       // menu behind ☰
        Assert.Equal(ToolbarMode.Contextual, o.ToolbarMode);
        Assert.True(o.ShowOmnibar);
        Assert.Equal(240, o.OutlineWidth); // etalon outline sidebar width
        Assert.Equal(ReadingWidth.Comfort, o.ReadingWidth); // comfortable centered column by default
        Assert.Equal(SplitOrientation.Horizontal, o.SplitOrientation); // side-by-side by default
        Assert.Equal(0.5, o.SplitRatio); // even split by default
    }

    [Fact]
    public void FromSettings_RoundTripsThroughToSettings()
    {
        var s = new LayoutSettings
        {
            MenuPlacement = MenuPlacement.Bar,
            ToolbarMode = ToolbarMode.Fixed,
            ShowOmnibar = false,
            OutlineWidth = 320,
            ReadingWidth = ReadingWidth.Narrow,
            SplitOrientation = SplitOrientation.Vertical,
            SplitRatio = 0.7,
        };

        Assert.Equal(s, LayoutOptions.FromSettings(s).ToSettings());
    }

    [Fact]
    public void ReadingWidthConverter_MapsPresetsToColumnAndAlignment()
    {
        var c = ReadingWidthConverter.Instance;
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Equal(double.PositiveInfinity, c.Convert(ReadingWidth.Full, typeof(double), null, culture));
        Assert.Equal(760d, c.Convert(ReadingWidth.Comfort, typeof(double), null, culture));
        Assert.Equal(620d, c.Convert(ReadingWidth.Narrow, typeof(double), null, culture));
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Stretch,
            c.Convert(ReadingWidth.Full, typeof(object), "align", culture));
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Center,
            c.Convert(ReadingWidth.Narrow, typeof(object), "align", culture));
    }

    [Theory]
    [InlineData(100, 180)]   // below min → clamped up
    [InlineData(500, 480)]   // above max → clamped down
    [InlineData(300, 300)]   // in range → unchanged
    public void OutlineWidth_IsClampedToRange(double set, double expected)
    {
        var o = new LayoutOptions { OutlineWidth = set };

        Assert.Equal(expected, o.OutlineWidth);
    }

    [Theory]
    [InlineData(0.05, 0.15)]  // below min → clamped up
    [InlineData(0.95, 0.85)]  // above max → clamped down
    [InlineData(0.5, 0.5)]    // in range → unchanged
    public void SplitRatio_IsClampedToRange(double set, double expected)
    {
        var o = new LayoutOptions { SplitRatio = set };

        Assert.Equal(expected, o.SplitRatio);
    }
}
