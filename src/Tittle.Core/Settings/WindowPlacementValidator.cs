using System;
using System.Collections.Generic;

namespace Tittle.Core.Settings;

/// <summary>A monitor's working area in screen pixels — an Avalonia-free mirror of
/// <c>Screen.WorkingArea</c> so the visibility check below stays in Core and is unit-testable.</summary>
public readonly record struct ScreenArea(int X, int Y, int Width, int Height);

/// <summary>
/// Decides whether a saved <see cref="WindowPlacement"/> is still usable on the current monitors.
/// A placement is "visible" when its rectangle overlaps some screen's working area by at least a
/// grabbable margin (so the title bar can be reached); otherwise the caller should re-centre — this
/// guards against restoring a window onto a monitor that has since been disconnected.
/// </summary>
public static class WindowPlacementValidator
{
    // Minimum on-screen overlap to consider the window reachable (~ a slice of the title bar).
    private const int MinVisibleWidth = 100;
    private const int MinVisibleHeight = 30;

    public static bool IsVisible(WindowPlacement placement, IReadOnlyList<ScreenArea> screens)
    {
        foreach (var s in screens)
        {
            var overlapW = Overlap(placement.X, placement.Width, s.X, s.Width);
            var overlapH = Overlap(placement.Y, placement.Height, s.Y, s.Height);
            if (overlapW >= MinVisibleWidth && overlapH >= MinVisibleHeight)
                return true;
        }

        return false;
    }

    // Length of the 1-D overlap between [aStart, aStart+aLen) and [bStart, bStart+bLen).
    private static double Overlap(double aStart, double aLen, double bStart, double bLen)
    {
        var start = Math.Max(aStart, bStart);
        var end = Math.Min(aStart + aLen, bStart + bLen);
        return Math.Max(0, end - start);
    }
}
