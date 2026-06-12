using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

// cv-* code decorations (colorizer policy, themed palette, hover tooltips), the source minimap feed,
// and section folding for plain-text tabs. Split out of the DocumentView core; same class.
public partial class DocumentView
{
    private readonly CodeDecorationColorizer _cvColorizer = new();
    private bool _cvAttached;
    private readonly IndentGuideRenderer _indentGuides = new();
    private bool _indentGuidesAttached;
    private AvaloniaEdit.Folding.FoldingManager? _foldingManager;

    /// <summary>Feed the minimap the outline + current viewport band. Internal for tests.</summary>
    internal void RefreshMinimap()
    {
        if (_vm is not { ShowMinimap: true } || Source.Document is not { LineCount: > 0 } doc)
            return;

        var textView = Source.TextArea.TextView;
        var docHeight = Math.Max(1, textView.DocumentHeight);
        Minimap.Update(
            _vm.Outline, doc.LineCount,
            Math.Clamp(textView.ScrollOffset.Y / docHeight, 0, 1),
            Math.Clamp((textView.ScrollOffset.Y + textView.Bounds.Height) / docHeight, 0, 1));
    }

    // ---- cv-* code decorations (ported): colorizer policy, themed palette, hover tooltips ----

    // Only non-markdown tabs are decorated — same as the original's code-view-only rule.
    // Indent guides additionally skip plain text so prose isn't visually striped.
    private void UpdateCvDecorationPolicy()
    {
        var textView = Source.TextArea.TextView;

        var wantCv = _vm is { IsMarkdown: false };
        if (wantCv != _cvAttached)
        {
            if (wantCv)
                textView.LineTransformers.Add(_cvColorizer);
            else
                textView.LineTransformers.Remove(_cvColorizer);
            _cvAttached = wantCv;
        }

        var wantGuides = _vm is { IsMarkdown: false, IsPlainText: false };
        if (wantGuides != _indentGuidesAttached)
        {
            if (wantGuides)
                textView.BackgroundRenderers.Add(_indentGuides);
            else
                textView.BackgroundRenderers.Remove(_indentGuides);
            _indentGuidesAttached = wantGuides;
        }
    }

    private void RefreshCvPalette()
    {
        var palette = new Dictionary<CodeDecorationKind, IBrush>();
        foreach (var (kind, key) in CvBrushKeys)
        {
            if (TryBrush(key) is { } brush)
                palette[kind] = brush;
        }

        _cvColorizer.SetPalette(palette);
        _indentGuides.GuideBrush = TryBrush("IndentGuideBrush");
        Minimap.MarkerBrush = TryBrush("AccentBrush");
        Minimap.ViewportBrush = TryBrush("OutlineItemHoverBrush");
        Minimap.InvalidateVisual();
    }

    private static readonly (CodeDecorationKind Kind, string Key)[] CvBrushKeys =
    {
        (CodeDecorationKind.Timestamp, "CvTimestampBrush"),
        (CodeDecorationKind.Uuid, "CvUuidBrush"),
        (CodeDecorationKind.Mac, "CvMacBrush"),
        (CodeDecorationKind.Ip, "CvIpBrush"),
        (CodeDecorationKind.Email, "CvEmailBrush"),
        (CodeDecorationKind.Hash, "CvHashBrush"),
        (CodeDecorationKind.FilePath, "CvPathBrush"),
        (CodeDecorationKind.Todo, "CvTodoBrush"),
        (CodeDecorationKind.LogLevel, "CvLogBrush"),
        (CodeDecorationKind.HtmlEntity, "CvEntityBrush"),
        (CodeDecorationKind.Unit, "CvUnitBrush"),
        (CodeDecorationKind.Date, "CvDateBrush"),
    };

    private void OnSourceHover(object? sender, PointerEventArgs e)
    {
        if (!_cvAttached)
            return;

        var tip = CvTooltipAt(e.GetPosition(Source.TextArea.TextView));
        if (tip is not null)
        {
            ToolTip.SetTip(Source, tip);
            ToolTip.SetIsOpen(Source, true);
        }
        else
        {
            ToolTip.SetIsOpen(Source, false);
        }
    }

    /// <summary>Resolved tooltip of the decoration under a TextView-relative point, or null.
    /// Internal so headless tests can probe without synthesizing hover events.</summary>
    internal string? CvTooltipAt(Point viewPoint)
    {
        var textView = Source.TextArea.TextView;
        var position = textView.GetPosition(viewPoint + textView.ScrollOffset);
        if (position is null || Source.Document is not { } document)
            return null;

        var line = document.GetLineByNumber(position.Value.Line);
        var column = position.Value.Column - 1;
        var text = document.GetText(line);
        // P8: reuse the colorizer's version-memoized scan (the line was already scanned during render)
        // instead of a fresh CodeDecorations.ScanLine that bypasses the cache on every hover.
        return _cvColorizer.ScanCached(document.Version, line.LineNumber, text)
            .FirstOrDefault(d => d.Tooltip is not null && column >= d.Start && column < d.Start + d.Length)
            ?.Tooltip;
    }

    // P8 test seam: lets a headless test assert the hover tooltip reuses the colorizer's scan cache.
    internal CodeDecorationColorizer CvColorizer => _cvColorizer;

    // ---- section folding for text files (ported): the heading stays, the body collapses ----

    /// <summary>Installs/refreshes the folding margin for a plain-text tab with an outline.
    /// Internal so headless tests can drive it without waiting on the dispatcher post.</summary>
    internal void UpdateSectionFolding()
    {
        var wanted = _vm is { IsPlainText: true, HasOutline: true };
        if (!wanted)
        {
            if (_foldingManager is not null)
            {
                AvaloniaEdit.Folding.FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }

            return;
        }

        if (_vm is null || Source.Document is not { LineCount: > 0 } document)
            return;

        _foldingManager ??= AvaloniaEdit.Folding.FoldingManager.Install(Source.TextArea);
        var foldings = SectionFolding.Compute(_vm.Outline, document.LineCount)
            .Select(s => new AvaloniaEdit.Folding.NewFolding(
                document.GetLineByNumber(s.HeadingLine).EndOffset,
                document.GetLineByNumber(s.EndLine).EndOffset)
            { Name = " … " })
            .OrderBy(f => f.StartOffset)
            .ToList();
        _foldingManager.UpdateFoldings(foldings, -1);
    }

    private void OnFoldAllRequested(bool collapse)
    {
        if (_foldingManager is null)
            return;
        foreach (var folding in _foldingManager.AllFoldings)
            folding.IsFolded = collapse;
    }

    /// <summary>Folded state probe for tests: how many sections exist / are collapsed.</summary>
    internal (int Total, int Folded) FoldingState()
    {
        if (_foldingManager is null)
            return (0, 0);
        var total = 0;
        var folded = 0;
        foreach (var f in _foldingManager.AllFoldings)
        {
            total++;
            if (f.IsFolded)
                folded++;
        }

        return (total, folded);
    }
}
