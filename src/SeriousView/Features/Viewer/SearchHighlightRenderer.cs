using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

/// <summary>Tints every in-document search match behind the editor text and outlines the current one.
/// A background renderer is part of the TextView's own draw pass (like the TextMate colouring), so it
/// repaints correctly over the GPU surface — unlike an external overlay (see the go-to-line memory).</summary>
public sealed class SearchHighlightRenderer : IBackgroundRenderer
{
    private IReadOnlyList<MatchRange> _matches = Array.Empty<MatchRange>();
    private int _current = -1;
    private IBrush? _fill;
    private IPen? _currentPen;

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>Set the matches, the current index, and the themed brushes (resolved by the view so the
    /// colours follow the theme). The caller invalidates the layer to redraw.</summary>
    public void Update(IReadOnlyList<MatchRange> matches, int current, IBrush? fill, IPen? currentPen)
    {
        _matches = matches;
        _current = current;
        _fill = fill;
        _currentPen = currentPen;
    }

    /// <summary>Draw calls issued by the last <see cref="Draw"/> — a headless test seam proving we only
    /// build geometry for the on-screen subset, not the whole document's match set.</summary>
    internal int LastDrawCount { get; private set; }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        LastDrawCount = 0;
        if (_matches.Count == 0 || _fill is null)
            return;

        textView.EnsureVisualLines();
        var lines = textView.VisualLines;
        if (!textView.VisualLinesValid || lines.Count == 0)
            return;

        // Clip to the visible document offset window: Draw runs on EVERY Selection-layer repaint
        // (scroll, resize), so looping all matches and allocating geometry per match — most of which
        // come back null off-screen — scales with total matches, not visible ones. _matches is ordered
        // by offset and non-overlapping, so we binary-search the first match that could touch the window
        // and break once past it: O(visible), not O(total).
        var visibleStart = lines[0].FirstDocumentLine.Offset;
        var visibleEnd = lines[^1].LastDocumentLine.EndOffset;

        var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };
        for (var i = FirstMatchAtOrAfter(visibleStart); i < _matches.Count; i++)
        {
            var match = _matches[i];
            if (match.Offset > visibleEnd)
                break; // ordered → every later match starts even further past the window
            if (match.Offset + match.Length < visibleStart)
                continue; // ends before the window (a match spanning the top edge)

            builder.AddSegment(textView, new TextSegment { StartOffset = match.Offset, Length = match.Length });
            var geometry = builder.CreateGeometry();
            if (geometry is not null)
            {
                drawingContext.DrawGeometry(_fill, i == _current ? _currentPen : null, geometry);
                LastDrawCount++;
            }
        }
    }

    /// <summary>Binary-search the first match whose end reaches <paramref name="offset"/> — i.e. the
    /// earliest one that can still be visible (a match straddling the top edge ends ≥ visibleStart).</summary>
    private int FirstMatchAtOrAfter(int offset)
    {
        int lo = 0, hi = _matches.Count;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_matches[mid].Offset + _matches[mid].Length < offset)
                lo = mid + 1;
            else
                hi = mid;
        }
        return lo;
    }
}
