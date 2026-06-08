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
        };

        Assert.Equal(s, LayoutOptions.FromSettings(s).ToSettings());
    }
}
