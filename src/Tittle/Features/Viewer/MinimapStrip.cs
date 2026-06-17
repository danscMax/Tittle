using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Tittle.Core.Text;

namespace Tittle.Features.Viewer;

/// <summary>Code minimap (ported): a thin strip beside the source editor mapping the symbol
/// outline onto document space — level-1 symbols draw long ticks, deeper ones short — with a
/// translucent band for the current viewport. A click navigates to the proportional line.
/// Sits in the layout NEXT to the editor (never over it — overlays don't repaint there).</summary>
public sealed class MinimapStrip : Control
{
    private IReadOnlyList<HeadingOutline> _outline = Array.Empty<HeadingOutline>();
    private int _lineCount;
    private double _viewTopFraction;
    private double _viewBottomFraction;

    /// <summary>Symbol tick brush (themed; set by the view).</summary>
    public IBrush? MarkerBrush { get; set; }

    /// <summary>Viewport band brush (themed; set by the view).</summary>
    public IBrush? ViewportBrush { get; set; }

    /// <summary>Raised with the 1-based document line for a click on the strip.</summary>
    public event Action<int>? LineRequested;

    // H7 seam: counts repaints actually requested (a no-op Update doesn't bump it).
    internal int InvalidateCount { get; private set; }

    public void Update(IReadOnlyList<HeadingOutline> outline, int lineCount,
        double viewTopFraction, double viewBottomFraction)
    {
        // H7: this runs on every source-scroll frame. Skip the repaint when nothing Render reads has
        // moved — same outline, line count, and viewport band (within a sub-pixel epsilon) — so a
        // stationary or sub-pixel scroll costs nothing.
        const double eps = 0.0005;
        if (ReferenceEquals(_outline, outline) && _lineCount == lineCount
            && Math.Abs(_viewTopFraction - viewTopFraction) < eps
            && Math.Abs(_viewBottomFraction - viewBottomFraction) < eps)
            return;

        _outline = outline;
        _lineCount = lineCount;
        _viewTopFraction = viewTopFraction;
        _viewBottomFraction = viewBottomFraction;
        InvalidateCount++;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // A transparent rect keeps the whole strip hit-testable for clicks.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_lineCount <= 0)
            return;

        var height = Bounds.Height;
        var width = Bounds.Width;

        if (ViewportBrush is not null && _viewBottomFraction > _viewTopFraction)
        {
            var top = Math.Clamp(_viewTopFraction, 0, 1) * height;
            var bottom = Math.Clamp(_viewBottomFraction, 0, 1) * height;
            context.FillRectangle(ViewportBrush, new Rect(0, top, width, Math.Max(2, bottom - top)));
        }

        if (MarkerBrush is null)
            return;
        foreach (var symbol in _outline)
        {
            var y = YForLine(symbol.Line, _lineCount, height);
            var tick = symbol.Level <= 1 ? width * 0.7 : width * 0.4;
            context.FillRectangle(MarkerBrush, new Rect((width - tick) / 2, y, tick, 2));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_lineCount > 0 && Bounds.Height > 0)
        {
            LineRequested?.Invoke(LineForY(e.GetPosition(this).Y, _lineCount, Bounds.Height));
            e.Handled = true;
        }
    }

    /// <summary>Document line (1-based) → strip Y. Pure; internal for tests.</summary>
    internal static double YForLine(int line, int lineCount, double height)
        => (line - 1) / (double)Math.Max(1, lineCount - 1) * height;

    /// <summary>Strip Y → document line (1-based, clamped). Pure; internal for tests.</summary>
    internal static int LineForY(double y, int lineCount, double height)
        => Math.Clamp((int)Math.Round(y / Math.Max(1, height) * (lineCount - 1)) + 1, 1, Math.Max(1, lineCount));
}
