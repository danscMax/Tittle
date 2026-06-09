using System.Globalization;
using SeriousView.Core.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

public class ToolbarVisibilityConverterTests
{
    private static bool Toolbar(ToolbarMode mode, bool showSource, bool hasTabs)
        => (bool)ToolbarVisibilityConverter.Instance.Convert(
            new object?[] { mode, showSource, hasTabs }, typeof(bool), "toolbar", CultureInfo.InvariantCulture);

    private static bool Fallback(ToolbarMode mode, bool showSource, bool hasTabs)
        => (bool)ToolbarVisibilityConverter.Instance.Convert(
            new object?[] { mode, showSource, hasTabs }, typeof(bool), "statusfallback", CultureInfo.InvariantCulture);

    [Fact]
    public void Toolbar_Off_AlwaysHidden()
        => Assert.False(Toolbar(ToolbarMode.Off, showSource: true, hasTabs: true));

    [Fact]
    public void Toolbar_Contextual_FollowsSourceMode()
    {
        Assert.True(Toolbar(ToolbarMode.Contextual, showSource: true, hasTabs: true));
        Assert.False(Toolbar(ToolbarMode.Contextual, showSource: false, hasTabs: true)); // preview → hidden
    }

    [Fact]
    public void Toolbar_Fixed_ShowsWheneverTabsOpen()
    {
        Assert.True(Toolbar(ToolbarMode.Fixed, showSource: false, hasTabs: true));  // even in preview
        Assert.False(Toolbar(ToolbarMode.Fixed, showSource: false, hasTabs: false)); // no tabs → nothing to show
    }

    [Fact]
    public void StatusFallback_OnlyWhenOff_AndSource()
    {
        Assert.True(Fallback(ToolbarMode.Off, showSource: true, hasTabs: true));
        Assert.False(Fallback(ToolbarMode.Contextual, showSource: true, hasTabs: true)); // toolbar has them
        Assert.False(Fallback(ToolbarMode.Off, showSource: false, hasTabs: true));       // preview
    }

    [Fact]
    public void WrapNumbers_NeverInBothPlaces_NorLostInSource()
    {
        foreach (var mode in new[] { ToolbarMode.Off, ToolbarMode.Contextual, ToolbarMode.Fixed })
        {
            // Never duplicated: the toggles can't be visible in the toolbar AND the status-bar fallback.
            Assert.False(Toolbar(mode, showSource: true, hasTabs: true) && Fallback(mode, showSource: true, hasTabs: true));
            // Never lost: in source mode they're available somewhere (toolbar or fallback).
            Assert.True(Toolbar(mode, showSource: true, hasTabs: true) || Fallback(mode, showSource: true, hasTabs: true));
        }
    }
}
