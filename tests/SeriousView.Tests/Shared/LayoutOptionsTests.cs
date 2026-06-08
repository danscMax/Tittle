using SeriousView.Core.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

public class LayoutOptionsTests
{
    [Fact]
    public void FromSettings_Null_GivesEtalonDefaults()
    {
        var o = LayoutOptions.FromSettings(null);

        Assert.Equal(MenuPlacement.Hidden, o.MenuPlacement);       // menu behind ☰
        Assert.Equal(ToolbarMode.Contextual, o.ToolbarMode);
        Assert.Equal(ViewTogglePlacement.Tabs, o.ViewTogglePlacement);
        Assert.True(o.ShowOmnibar);
        Assert.False(o.ShowRail);
        Assert.True(o.ReadingMode);   // reading column on by default (etalon)
        Assert.Equal(240, o.OutlineWidth); // etalon outline sidebar width
    }

    [Fact]
    public void FromSettings_RoundTripsThroughToSettings()
    {
        var s = new LayoutSettings
        {
            MenuPlacement = MenuPlacement.Bar,
            ToolbarMode = ToolbarMode.Fixed,
            ViewTogglePlacement = ViewTogglePlacement.StatusBar,
            ShowOmnibar = false,
            ShowRail = true,
            ReadingMode = false,
            OutlineWidth = 320,
        };

        Assert.Equal(s, LayoutOptions.FromSettings(s).ToSettings());
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
}
