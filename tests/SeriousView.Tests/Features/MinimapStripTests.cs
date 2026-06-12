using System;
using Avalonia.Headless.XUnit;
using SeriousView.Core.Text;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

/// <summary>Guards H7: the minimap repaints only when the viewport band (or outline/line count)
/// actually moves, not on every source-scroll frame.</summary>
public class MinimapStripTests
{
    [AvaloniaFact]
    public void Update_UnchangedBand_DoesNotRepaint()
    {
        var strip = new MinimapStrip();
        var outline = Array.Empty<HeadingOutline>();

        strip.Update(outline, 100, 0.10, 0.30);
        var baseline = strip.InvalidateCount;

        strip.Update(outline, 100, 0.10, 0.30); // identical → no repaint
        Assert.Equal(baseline, strip.InvalidateCount);

        strip.Update(outline, 100, 0.10, 0.300001); // sub-epsilon nudge → still no repaint
        Assert.Equal(baseline, strip.InvalidateCount);

        strip.Update(outline, 100, 0.10, 0.31); // band moved → one repaint
        Assert.Equal(baseline + 1, strip.InvalidateCount);

        strip.Update(outline, 120, 0.10, 0.31); // line count changed → one repaint
        Assert.Equal(baseline + 2, strip.InvalidateCount);
    }
}
