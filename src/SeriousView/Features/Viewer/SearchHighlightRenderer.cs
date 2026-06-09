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

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0 || _fill is null)
            return;

        textView.EnsureVisualLines();
        for (var i = 0; i < _matches.Count; i++)
        {
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };
            builder.AddSegment(textView, new TextSegment { StartOffset = _matches[i].Offset, Length = _matches[i].Length });
            var geometry = builder.CreateGeometry();
            if (geometry is not null)
                drawingContext.DrawGeometry(_fill, i == _current ? _currentPen : null, geometry);
        }
    }
}
