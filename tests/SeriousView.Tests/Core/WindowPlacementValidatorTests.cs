using System;
using System.Collections.Generic;
using SeriousView.Core.Settings;
using Xunit;

namespace SeriousView.Tests.Core;

public class WindowPlacementValidatorTests
{
    private static readonly IReadOnlyList<ScreenArea> Single = new[] { new ScreenArea(0, 0, 1920, 1080) };

    [Fact]
    public void FullyInside_IsVisible()
    {
        var p = new WindowPlacement(800, 600, 100, 100, false);
        Assert.True(WindowPlacementValidator.IsVisible(p, Single));
    }

    [Fact]
    public void CompletelyOffScreen_IsNotVisible()
    {
        var p = new WindowPlacement(800, 600, 5000, 5000, false);
        Assert.False(WindowPlacementValidator.IsVisible(p, Single));
    }

    [Fact]
    public void OnlyATinySliverOnScreen_IsNotVisible()
    {
        // ~10px peeking from the right edge — below the grabbable margin.
        var p = new WindowPlacement(800, 600, 1910, 100, false);
        Assert.False(WindowPlacementValidator.IsVisible(p, Single));
    }

    [Fact]
    public void OnSecondMonitor_IsVisible()
    {
        var screens = new[]
        {
            new ScreenArea(0, 0, 1920, 1080),
            new ScreenArea(1920, 0, 1920, 1080),
        };
        var p = new WindowPlacement(800, 600, 2200, 100, false);
        Assert.True(WindowPlacementValidator.IsVisible(p, screens));
    }

    [Fact]
    public void NoScreens_IsNotVisible()
    {
        var p = new WindowPlacement(800, 600, 0, 0, false);
        Assert.False(WindowPlacementValidator.IsVisible(p, Array.Empty<ScreenArea>()));
    }
}
