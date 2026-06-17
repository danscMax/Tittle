using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using Tittle.Core.Text;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>Proves the version-cache: the per-line scan runs once per line per document Version
/// (repeated redraws don't re-scan), an edit (Version bump) re-scans, and a palette/theme swap
/// keeps the cached scan (the scan is palette-independent).</summary>
public class CodeDecorationColorizerTests
{
    private static CodeDecorationColorizer MakeColorizer()
    {
        var colorizer = new CodeDecorationColorizer { Today = () => new DateOnly(2026, 6, 11) };
        colorizer.SetPalette(new Dictionary<CodeDecorationKind, IBrush>
        {
            [CodeDecorationKind.Todo] = Brushes.Red,
            [CodeDecorationKind.LogLevel] = Brushes.Orange,
        });
        return colorizer;
    }

    [Fact]
    public void RepeatedRedrawsAtSameVersion_ScanEachLineOnce()
    {
        var doc = new TextDocument("// TODO one\n[ERROR] two\nplain three");
        var colorizer = MakeColorizer();
        var version = doc.Version;

        // Simulate three full redraws over the same three visible lines without any edit.
        for (var redraw = 0; redraw < 3; redraw++)
            for (var n = 1; n <= 3; n++)
            {
                var line = doc.GetLineByNumber(n);
                colorizer.ScanCached(version, n, doc.GetText(line));
            }

        // 3 distinct lines, scanned once each — not 9.
        Assert.Equal(3, colorizer.ScanCount);
    }

    [Fact]
    public void VersionBump_AfterEdit_ReScans()
    {
        var doc = new TextDocument("// TODO one");
        var colorizer = MakeColorizer();

        var v1 = doc.Version;
        colorizer.ScanCached(v1, 1, doc.GetText(doc.GetLineByNumber(1)));
        Assert.Equal(1, colorizer.ScanCount);

        // Edit advances the document version; the memo must drop and re-scan.
        doc.Insert(0, "x ");
        var v2 = doc.Version;
        Assert.False(ReferenceEquals(v1, v2));

        colorizer.ScanCached(v2, 1, doc.GetText(doc.GetLineByNumber(1)));
        Assert.Equal(2, colorizer.ScanCount);
    }

    [Fact]
    public void PaletteSwap_DoesNotInvalidateCachedScan()
    {
        var doc = new TextDocument("[ERROR] boom");
        var colorizer = MakeColorizer();
        var version = doc.Version;

        colorizer.ScanCached(version, 1, doc.GetText(doc.GetLineByNumber(1)));
        Assert.Equal(1, colorizer.ScanCount);

        // A theme/palette swap (DocumentView.RefreshCvPalette) forces a Redraw but does NOT bump
        // the document Version — the scan (offsets + kinds) is palette-independent, so it stays cached.
        colorizer.SetPalette(new Dictionary<CodeDecorationKind, IBrush>
        {
            [CodeDecorationKind.LogLevel] = Brushes.Yellow,
        });
        colorizer.ScanCached(version, 1, doc.GetText(doc.GetLineByNumber(1)));

        Assert.Equal(1, colorizer.ScanCount);
    }

    [Fact]
    public void CachedScan_ReturnsSameDecorations()
    {
        var doc = new TextDocument("[ERROR] 10.0.0.1 TODO");
        var colorizer = MakeColorizer();
        var version = doc.Version;
        var text = doc.GetText(doc.GetLineByNumber(1));

        var first = colorizer.ScanCached(version, 1, text);
        var second = colorizer.ScanCached(version, 1, text);

        // Same memoized instance on the second call, and it agrees with a fresh pure scan.
        Assert.Same(first, second);
        var expected = CodeDecorations.ScanLine(text, new DateOnly(2026, 6, 11));
        Assert.Equal(expected.Count, first.Count);
    }
}
