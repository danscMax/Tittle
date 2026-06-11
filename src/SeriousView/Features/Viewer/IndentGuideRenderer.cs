using System;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Rendering;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

/// <summary>Draws vertical indent guides behind the code view at every tab stop inside a
/// line's leading whitespace (ported). Geometry is pure Core (IndentGuides) — blank lines
/// bridge the surrounding block; plain-text and markdown tabs don't get striped.</summary>
public sealed class IndentGuideRenderer : IBackgroundRenderer
{
    // Reading a multi-megabyte minified line just to measure its indent would hurt scrolling;
    // an indent longer than this reads as blank, which only affects pathological files.
    private const int MaxIndentProbe = 256;

    private IPen? _pen;
    private IBrush? _guideBrush;

    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>Themed guide brush; set by the view on load and on theme change.</summary>
    public IBrush? GuideBrush
    {
        get => _guideBrush;
        set
        {
            _guideBrush = value;
            _pen = value is null ? null : new Pen(value, 1);
        }
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_pen is null || textView.Document is not { } document || textView.VisualLines.Count == 0)
            return;

        var tabSize = Math.Max(1, textView.Options?.IndentationSize ?? 4);
        string? LineAt(int n)
        {
            if (n < 1 || n > document.LineCount)
                return null;
            var line = document.GetLineByNumber(n);
            return document.GetText(line.Offset, Math.Min(line.Length, MaxIndentProbe));
        }

        foreach (var visualLine in textView.VisualLines)
        {
            var columns = IndentGuides.EffectiveColumns(LineAt, visualLine.FirstDocumentLine.LineNumber, tabSize);
            var top = visualLine.VisualTop - textView.ScrollOffset.Y;
            var bottom = top + visualLine.Height;
            foreach (var column in IndentGuides.GuideColumnsFor(columns, tabSize))
            {
                // Half-pixel alignment keeps the 1px line crisp.
                var x = Math.Round(column * textView.WideSpaceWidth - textView.ScrollOffset.X) + 0.5;
                drawingContext.DrawLine(_pen, new Point(x, top), new Point(x, bottom));
            }
        }
    }
}
