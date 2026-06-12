using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeriousView.Core.Text;
using SeriousView.Features.Shell;

namespace SeriousView.Features.Viewer;

// Scroll-spy, position sync on the preview↔source toggle, the heading-Y cache, active-heading
// tracking, TOC/go-to-line navigation and source scrolling. Split out of the DocumentView core; same class.
public partial class DocumentView
{
    // --- Position sync on the preview↔source toggle (M10). The reading position is anchored
    //     as (nearest heading above the viewport top, fractional progress to the next one) and
    //     restored in the other mode — pure maths in Core/Text/HeadingAnchors. Sync only ever
    //     scrolls; the caret moves on explicit navigation (TOC, go-to-line, find), not here. ---

    // Stale-closure guard: navigation bumps the generation so a pending sync (posted or armed
    // on LayoutUpdated) can never fight a TOC jump that follows it.
    private int _syncGeneration;
    private EventHandler? _previewRetryHandler;

    private void CancelPendingSync()
    {
        _syncGeneration++;
        UnhookPreviewRetry();
    }

    private void UnhookPreviewRetry()
    {
        if (_previewRetryHandler is { } handler)
        {
            Preview.LayoutUpdated -= handler;
            _previewRetryHandler = null;
        }
    }

    // Test seam: counts sync runs that pass the early-return (R8 asserts a hidden tab does none).
    internal int SyncRunCount { get; private set; }

    private void SyncPositionAcrossModes()
    {
        if (_vm is null || !_vm.IsMarkdown || _vm.ShowNotice)
            return;

        SyncRunCount++;
        FlushPendingPreviewReflow(); // fresh heading Ys before capturing/applying across modes

        var gen = ++_syncGeneration;
        // Apply AFTER the newly shown view has laid out (Loaded runs below the render/layout pass),
        // else the target scroller still reports a stale/zero extent and clamps the offset away.
        // Loaded (not Background) lands the scroll in the same post-layout cycle as the first paint,
        // so the page never paints at the top and then jumps to the restored position.
        if (_vm.ShowSource)
        {
            if (CaptureFromPreview() is { } anchor)
                Dispatcher.UIThread.Post(() =>
                {
                    if (gen == _syncGeneration)
                        ApplyToSource(anchor);
                }, DispatcherPriority.Loaded);
        }
        else
        {
            var anchor = CaptureFromSource();
            Dispatcher.UIThread.Post(() =>
            {
                if (gen == _syncGeneration)
                    ApplyToPreview(anchor, retryAfterLayout: true);
            }, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Anchor of the preview reading position (probe = just under the chrome). The
    /// heading tops stay valid while the preview is hidden — offsets persist on kept-alive
    /// views. Null when the preview never laid out (nothing worth syncing from).</summary>
    private HeadingAnchor? CaptureFromPreview()
        => PreviewScroll.Extent.Height > 0 && EnsurePreviewHeadingTops() is { } tops
            ? HeadingAnchors.FromPosition(
                tops, PreviewScroll.Offset.Y + PreviewScroll.Padding.Top + 1, PreviewScroll.Extent.Height)
            : null;

    /// <summary>Anchor of the source reading position (probe = first visible line).</summary>
    private HeadingAnchor CaptureFromSource()
    {
        if (_vm is null || Source.Document is not { LineCount: > 0 } doc)
            return new HeadingAnchor(-1, 0);

        var textView = Source.TextArea.TextView;
        var y = Math.Clamp(textView.ScrollOffset.Y + 1, 0, Math.Max(0, textView.DocumentHeight - 1));
        var line = textView.GetDocumentLineByVisualTop(y).LineNumber;
        return HeadingAnchors.FromLine(_vm.Outline, line, doc.LineCount);
    }

    private void ApplyToSource(HeadingAnchor anchor)
    {
        if (_vm is null || Source.Document is not { LineCount: > 0 } doc || SourceScroller is not { } scroller)
            return;

        var line = HeadingAnchors.ToLine(_vm.Outline, anchor, doc.LineCount);
        var top = Math.Max(0, Source.TextArea.TextView.GetVisualTopByDocumentLine(line));
        scroller.Offset = scroller.Offset.WithY(top);
        RecomputeActiveHeading();
    }

    // --- Active-heading tracking (scroll-spy, M10): the heading at the viewport top feeds the
    //     outline marker and the breadcrumbs through the tab VM (written like CaretLine). ---

    /// <summary>Probe slack below the chrome line: a heading's own top margin keeps its control
    /// a few px below the scrolled-to position, and the slack keeps it counted as active.</summary>
    private const double PreviewActiveProbeSlack = 24.0;

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // An extent change means the rendered markdown reflowed (first layout, images, zoom,
        // resize) → cached heading Ys are stale. Rather than rebuild + re-walk the whole preview on
        // EVERY frame of a resize drag, the first layout primes immediately and later reflows are
        // coalesced onto a short debounce (see SchedulePreviewReflow). The cache is deliberately
        // kept (not nulled) between schedule and trailing run, so the scroll-spy below stays a cheap
        // binary search during the drag; the trailing run refreshes it.
        if (e.ExtentDelta.Y != 0)
            MaybeScheduleReflowOnExtentChange();
        if (_vm is { IsActive: true, ShowPreview: true })
        {
            RecomputeActiveHeading();
            if (CaptureFromPreview() is { } anchor)
                _vm.ReadingAnchor = anchor; // live reading position, for reload restore (M14)
            _vm.ScrollPercentText = FormatScrollPercent(
                PreviewScroll.Offset.Y, PreviewScroll.Extent.Height, PreviewScroll.Viewport.Height);
        }

        // Back-to-top appears once the reader is a screen below the start (ported).
        BackToTopButton.IsVisible = PreviewScroll.Offset.Y > PreviewScroll.Viewport.Height;
    }

    private void OnSourceScrollChanged(object? sender, EventArgs e)
    {
        if (_vm is { IsActive: true, ShowSource: true })
        {
            RecomputeActiveHeading();
            _vm.ReadingAnchor = CaptureFromSource();
            if (SourceScroller is { } scroller)
                _vm.ScrollPercentText = FormatScrollPercent(
                    scroller.Offset.Y, scroller.Extent.Height, scroller.Viewport.Height);
            RefreshMinimap();
        }
    }

    /// <summary>"NN%" through the document, or empty when it fits the viewport (ported).</summary>
    private static string FormatScrollPercent(double offset, double extent, double viewport)
    {
        var max = extent - viewport;
        if (max < 1)
            return string.Empty;
        return $"{Math.Clamp((int)Math.Round(offset / max * 100), 0, 100)}%";
    }

    /// <summary>Scroll-spy recompute — a binary search over cached positions, cheap enough to
    /// run unthrottled per scroll event. Internal so headless tests can poke it directly.
    /// Non-markdown tabs participate too (ported "code breadcrumbs"): their symbol/text
    /// outlines are line-based, so the source branch drives the marker and the crumbs.</summary>
    internal void RecomputeActiveHeading()
    {
        if (_vm is null || !_vm.HasOutline)
            return;

        if (_vm.IsMarkdown && _vm.ShowPreview)
        {
            if (EnsurePreviewHeadingTops() is { } tops)
                _vm.ActiveHeadingOrdinal = HeadingAnchors.ActiveOrdinal(
                    tops, PreviewScroll.Offset.Y + PreviewScroll.Padding.Top, PreviewActiveProbeSlack);
        }
        else if (Source.Document is { LineCount: > 0 })
        {
            var textView = Source.TextArea.TextView;
            var y = Math.Clamp(textView.ScrollOffset.Y + 1, 0, Math.Max(0, textView.DocumentHeight - 1));
            _vm.ActiveHeadingOrdinal = HeadingAnchors.ActiveOrdinalForLine(
                _vm.Outline, textView.GetDocumentLineByVisualTop(y).LineNumber);
        }
    }

    private void ApplyToPreview(HeadingAnchor anchor, bool retryAfterLayout)
    {
        if (PreviewScroll.Extent.Height > 0 && EnsurePreviewHeadingTops() is { } tops)
        {
            var position = HeadingAnchors.ToPosition(tops, anchor, PreviewScroll.Extent.Height);
            var max = Math.Max(0, PreviewScroll.Extent.Height - PreviewScroll.Viewport.Height);
            PreviewScroll.Offset = PreviewScroll.Offset.WithY(
                Math.Clamp(position - PreviewScroll.Padding.Top - 1, 0, max));
            RecomputeActiveHeading();
        }
        else if (retryAfterLayout)
        {
            // First-ever preview layout (markdown toggled to source before the preview showed):
            // retry once after the markdown control lays out. Self-unsubscribing; cancelled by
            // navigation/detach via the generation + UnhookPreviewRetry.
            var gen = _syncGeneration;
            UnhookPreviewRetry();
            _previewRetryHandler = (_, _) =>
            {
                UnhookPreviewRetry();
                if (gen == _syncGeneration)
                    ApplyToPreview(anchor, retryAfterLayout: false);
            };
            Preview.LayoutUpdated += _previewRetryHandler;
        }
    }

    // The go-to-line request is raised by the status-bar input (wired in MainWindow); scroll there.
    // R13/Q14: capture the VM + generation and re-check in the posted lambda (mirrors RestoreAnchor),
    // so closing/swapping the tab in the micro-window doesn't scroll a now-foreign editor.
    private void OnGoToLineRequested(int line)
    {
        var vm = _vm;
        var gen = ++_syncGeneration;
        Dispatcher.UIThread.Post(() =>
        {
            if (gen != _syncGeneration || !ReferenceEquals(vm, _vm))
                return;
            ScrollSourceToLine(line);
            Source.TextArea.Focus();
        });
    }

    private void OnNavigationRequested(HeadingOutline heading)
    {
        if (_vm is null)
            return;

        CancelPendingSync(); // an explicit jump always beats a pending mode-toggle sync
        FlushPendingPreviewReflow(); // settle a mid-drag reflow so heading Ys are fresh for the jump

        // In preview, scroll the rendered document in place; if the heading control can't be
        // located, fall back to the reliable line-based source scroll (switching mode first).
        if (_vm.ShowPreview)
        {
            if (TryScrollPreviewToHeading(heading.Ordinal))
                return;
            _vm.ViewMode = DocumentViewMode.Source;
        }

        // Defer so the editor is laid out (and visible after a mode switch) before scrolling.
        Dispatcher.UIThread.Post(() => ScrollSourceToLine(heading.Line));
    }

    // Heading-Y cache: the walk is the expensive part, so it runs once per layout generation —
    // invalidated only when the preview extent changes (reflow/images/zoom/first layout), after
    // which per-scroll work is a binary search (no debounce needed).
    private List<double>? _previewHeadingTops;

    private IReadOnlyList<double>? EnsurePreviewHeadingTops()
        => _previewHeadingTops ??= ComputePreviewHeadingTops();

    /// <summary>Content-space Y of every preview heading, by walking the rendered tree
    /// (Markdown.Avalonia exposes no scroll/heading API). Scroll-invariant: viewport-relative
    /// TranslatePoint plus the current Offset. Null while the preview hasn't laid out. Index
    /// order matches <see cref="DocumentTabViewModel.Outline"/> — the same contract the M4
    /// navigation has relied on (both walks skip admonition-nested headings).</summary>
    private List<double>? ComputePreviewHeadingTops()
        => ComputePreviewHeadingTops(
            Preview.GetVisualDescendants().OfType<Control>().Where(IsTopLevelHeading).ToList());

    // Heading Ys from a pre-collected, document-ordered heading list (the reflow pass shares its
    // single traversal; the lazy cache-miss path collects its own). Viewport-relative TranslatePoint
    // + current Offset = scroll-invariant. Index order matches the Core outline.
    private List<double>? ComputePreviewHeadingTops(IReadOnlyList<Control> headings)
    {
        var offsetY = PreviewScroll.Offset.Y;
        var tops = new List<double>(headings.Count);
        foreach (var heading in headings)
        {
            if (heading.TranslatePoint(default, PreviewScroll) is not { } p)
                return null;
            tops.Add(p.Y + offsetY);
        }

        return tops.Count == 0 ? null : tops;
    }

    /// <summary>Scroll the preview so the <paramref name="ordinal"/>-th heading sits at the
    /// viewport top, inset by the scroller's own top padding (the document-start look).
    /// BringIntoView was rejected — coming from above it parks the heading at the BOTTOM edge.
    /// Returns false when the preview has no laid-out headings yet.</summary>
    private bool TryScrollPreviewToHeading(int ordinal)
    {
        if (EnsurePreviewHeadingTops() is not { } tops || ordinal < 0 || ordinal >= tops.Count)
            return false;

        var max = Math.Max(0, PreviewScroll.Extent.Height - PreviewScroll.Viewport.Height);
        var target = Math.Clamp(tops[ordinal] - PreviewScroll.Padding.Top, 0, max);
        PreviewScroll.Offset = PreviewScroll.Offset.WithY(target);
        return true;
    }

    // Markdown.Avalonia renders headings as controls with a "Heading1".."Heading6" style class.
    // Headings inside an admonition callout are excluded so the order matches the Core outline
    // (which skips blockquoted headings). NB: depends on that class naming — recheck on upgrade.
    private static bool IsTopLevelHeading(Control control)
        => control.Classes.Any(c => c is "Heading1" or "Heading2" or "Heading3"
                                      or "Heading4" or "Heading5" or "Heading6")
        && !control.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("admonition"));

    // The editor's template ScrollViewer (cached — the view is kept alive, the template applies
    // once). TextEditor.ScrollToVerticalOffset proved a silent no-op here, so scrolling goes
    // straight to the scroller; it clamps the offset itself.
    private ScrollViewer? _sourceScroller;

    private ScrollViewer? SourceScroller =>
        _sourceScroller ??= Source.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    private void ScrollSourceToLine(int line1Based)
    {
        var caret = Source.TextArea.Caret;
        caret.Line = line1Based;
        caret.Column = 1;
        // Land the line at the viewport TOP: ScrollToLine centers it (VisualYPosition.LineMiddle)
        // and BringCaretToView only nudges the nearest edge — neither reads as "go to" (M10).
        var top = Math.Max(0, Source.TextArea.TextView.GetVisualTopByDocumentLine(line1Based));
        if (SourceScroller is { } scroller)
            scroller.Offset = scroller.Offset.WithY(top);
    }
}
