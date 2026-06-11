using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

/// <summary>Paints cv-* token foregrounds (timestamps, ips, TODOs, units…) over the TextMate
/// highlighting in non-markdown source views. Token scanning is pure Core (CodeDecorations);
/// this transformer only maps kinds to themed brushes supplied by the view.</summary>
public sealed class CodeDecorationColorizer : DocumentColorizingTransformer
{
    private IReadOnlyDictionary<CodeDecorationKind, IBrush> _palette =
        new Dictionary<CodeDecorationKind, IBrush>();

    /// <summary>Today's date for the relative-date tooltips/scans; injectable for tests.</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

    // ColorizeLine runs per visible line per redraw — querying the OS clock thousands of
    // times a second for a value that changes at midnight is waste. Refresh once a minute.
    private DateOnly _todayCache;
    private long _todayStampMs = long.MinValue;

    // ScanLine allocates a string + regex matches + records per call, and ColorizeLine runs once
    // per visible line on EVERY redraw of a code/text tab — scrolling a static file would re-scan
    // every frame. Memoize the (palette-independent) scan per line, invalidated when the document
    // version advances (any edit) — mirrors IndentGuideRenderer's idiom. The palette/theme swap
    // path (SetPalette → Redraw) is safe to keep cached: the SCAN result (offsets + kinds) does
    // not depend on the palette; only the brush lookup below does, and that re-runs every redraw.
    private readonly Dictionary<int, IReadOnlyList<CodeDecoration>> _scanCache = new();
    private ITextSourceVersion? _scanVersion;

    // Test seam: counts the (uncached) scans so a test can prove repeated redraws at the same
    // document Version don't re-scan, while a Version bump (edit) does.
    internal int ScanCount { get; private set; }

    private DateOnly CachedToday()
    {
        var now = Environment.TickCount64;
        if (now - _todayStampMs > 60_000)
        {
            _todayCache = Today();
            _todayStampMs = now;
        }

        return _todayCache;
    }

    /// <summary>Swaps the themed brushes (called by the view on load and on theme change).</summary>
    public void SetPalette(IReadOnlyDictionary<CodeDecorationKind, IBrush> palette) => _palette = palette;

    // One bold typeface per (family, style) seen — hoisted out of the per-hit ChangeLinePart
    // callback so a TODO/log-heavy line doesn't allocate a fresh Typeface per matched token.
    private readonly Dictionary<(FontFamily, FontStyle), Typeface> _boldTypefaces = new();

    private Typeface BoldOf(Typeface tf)
    {
        var key = (tf.FontFamily, tf.Style);
        if (!_boldTypefaces.TryGetValue(key, out var bold))
        {
            bold = new Typeface(tf.FontFamily, tf.Style, FontWeight.Bold);
            _boldTypefaces[key] = bold;
        }

        return bold;
    }

    /// <summary>Per-line scan with version-keyed memoization. Drops the memo when the document
    /// version (advances on edit) changes — empty results are cheap to cache (ScanLine returns a
    /// shared empty array on no-hit lines). Exposed internally so tests can drive it directly.</summary>
    internal IReadOnlyList<CodeDecoration> ScanCached(ITextSourceVersion? version, int lineNumber, string text)
    {
        if (!ReferenceEquals(version, _scanVersion))
        {
            _scanCache.Clear();
            _scanVersion = version;
        }

        if (_scanCache.TryGetValue(lineNumber, out var cached))
            return cached;

        ScanCount++;
        var scanned = CodeDecorations.ScanLine(text, CachedToday());
        _scanCache[lineNumber] = scanned;
        return scanned;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0 || line.Length > CodeDecorations.MaxLineLength || _palette.Count == 0)
            return;

        var document = CurrentContext.Document;
        var text = document.GetText(line);
        foreach (var d in ScanCached(document.Version, line.LineNumber, text))
        {
            if (!_palette.TryGetValue(d.Kind, out var brush))
                continue;

            var bold = d.Kind is CodeDecorationKind.Todo or CodeDecorationKind.LogLevel;
            ChangeLinePart(line.Offset + d.Start, line.Offset + d.Start + d.Length, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
                if (bold)
                    element.TextRunProperties.SetTypeface(BoldOf(element.TextRunProperties.Typeface));
            });
        }
    }
}
