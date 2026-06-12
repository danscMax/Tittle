using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
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

    // EffectiveColumns scans up to ±BlankScanLimit neighbours for a blank line; Draw runs every
    // frame, so on a blank-heavy file an uncached scroll re-walks ~200 lines per visible blank line.
    // Memoize per line, invalidated when the document version (advances on edit) or tab size changes.
    // P9: a never-edited document keeps the same version, so without a cap the memo grows by one entry
    // per line scrolled — unbounded for a huge static file. Cap it FIFO (insertion order ≈ scroll
    // order, so the evicted entry is the farthest off-screen); eviction is O(1).
    private const int CacheCap = 4096;
    private readonly Dictionary<int, int> _effectiveCache = new();
    private readonly Queue<int> _cacheOrder = new();
    private ITextSourceVersion? _cacheVersion;
    private int _cacheTabSize = -1;

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
        var version = document.Version;

        string? LineAt(int n)
        {
            if (n < 1 || n > document.LineCount)
                return null;
            var line = document.GetLineByNumber(n);
            return document.GetText(line.Offset, Math.Min(line.Length, MaxIndentProbe));
        }

        foreach (var visualLine in textView.VisualLines)
        {
            var columns = EffectiveColumns(LineAt, visualLine.FirstDocumentLine.LineNumber, tabSize, version);
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

    /// <summary>Effective indent columns for a line, version+tab-size memoized and FIFO-capped.
    /// Internal so a test can drive the cache directly without a TextView.</summary>
    internal int EffectiveColumns(Func<int, string?> lineAt, int lineNumber, int tabSize,
        ITextSourceVersion? version)
    {
        // Drop the memo when the text changed (Version advances on edit) or the tab size moved.
        if (tabSize != _cacheTabSize || !ReferenceEquals(version, _cacheVersion))
        {
            _effectiveCache.Clear();
            _cacheOrder.Clear();
            _cacheVersion = version;
            _cacheTabSize = tabSize;
        }

        if (_effectiveCache.TryGetValue(lineNumber, out var cached))
            return cached;

        if (_effectiveCache.Count >= CacheCap && _cacheOrder.Count > 0)
            _effectiveCache.Remove(_cacheOrder.Dequeue()); // evict the oldest (farthest off-screen)

        var computed = IndentGuides.EffectiveColumns(lineAt, lineNumber, tabSize);
        _effectiveCache[lineNumber] = computed;
        _cacheOrder.Enqueue(lineNumber);
        return computed;
    }

    /// <summary>Test seam: the bounded per-line memo's current size.</summary>
    internal int CacheCount => _effectiveCache.Count;
}
