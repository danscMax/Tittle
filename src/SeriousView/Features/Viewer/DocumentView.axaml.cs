using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Rendering;
using SeriousView.Core.Text;
using SeriousView.Features.Shell;
using MdEngine = Markdown.Avalonia.Markdown;

namespace SeriousView.Features.Viewer;

/// <summary>Per-tab body: switches between the source editor and the rendered
/// markdown preview (driven by the tab VM's ShowSource/ShowPreview), and scrolls
/// to a heading when the outline raises a navigation request.</summary>
public partial class DocumentView : UserControl
{
    private DocumentTabViewModel? _vm;
    private readonly SearchHighlightRenderer _searchRenderer = new();
    private bool _searchRendererAttached;
    private readonly CodeDecorationColorizer _cvColorizer = new();
    private bool _cvAttached;
    private readonly IndentGuideRenderer _indentGuides = new();
    private bool _indentGuidesAttached;
    private AvaloniaEdit.Folding.FoldingManager? _foldingManager;

    // Debounce for the heavy preview-reflow passes (heading-Y rebuild, embedded code-editor height
    // pinning, sorter/collapser attach). A resize drag raises ScrollChanged every frame; without
    // this they ran 3-4 full visual-tree walks per frame — the resize lag, worst in preview. The
    // first layout runs them immediately (the opened document is complete at once); every later
    // reflow is coalesced into one trailing run. Reset per content (DataContext change).
    private readonly DispatcherTimer _previewReflowTimer;
    private bool _previewReflowPrimed;
    private int _previewReflowPassCount;

    // Resize coalescing for the preview. Markdown.Avalonia does NOT virtualise: the whole document is
    // realised, so Avalonia re-measures every block on EVERY width change — a resize drag re-lays-out
    // the entire document per frame (measured ~68ms/step on a 120-section doc vs ~0.1ms in the
    // virtualised source editor). A control with an explicit Width caches its measure, so while a
    // resize is in flight we PIN Preview.Width (no re-wrap, 0 re-layouts) and release it once on
    // settle (one re-layout). Reset per content in Unsubscribe.
    private readonly DispatcherTimer _resizeSettleTimer;
    private bool _previewFrozen;

    public DocumentView()
    {
        InitializeComponent();

        _previewReflowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _previewReflowTimer.Tick += OnPreviewReflowTick;
        _resizeSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _resizeSettleTimer.Tick += OnResizeSettleTick;

        // Configure the existing engine (rather than replace it) so the auto-selected
        // theme-aware FluentAvalonia style stays intact:
        //  - harden links — the default command shell-executes any scheme (file://,
        //    custom handlers) from untrusted documents;
        //  - render ::: admonition-* containers (from the Core preprocessor) as callouts.
        if (Preview.Engine is MdEngine engine)
        {
            // wiki: links open the sibling note via the shell (M10); everything else stays on
            // the safe http/https/mailto policy. Providers read _vm live (DataContext-tracked).
            engine.HyperlinkCommand = new WikiHyperlinkCommand(
                () => _vm?.AssetPathRoot,
                path =>
                {
                    if (_vm?.Shell is { } shell)
                        _ = shell.OpenPathAsync(path);
                },
                SafeHyperlinkCommand.Instance);
            engine.ContainerBlockHandler = new AdmonitionBlockHandler(engine);
        }

        // Child-control / self subscriptions are wired once here and torn down on final detach
        // (DetachChildHandlers) — the publishers (editor caret, preview scroll, minimap, hover)
        // outlive a single DataContext, so a closed tab must leave no live delegates. Lambdas became
        // named handlers so each += has a matching -=.
        Source.TextArea.Caret.PositionChanged += OnCaretPositionChanged; // caret → status bar
        Source.TextArea.SelectionChanged += OnSelectionChanged;          // selection word count (ported)
        // Scroll-spy (M10): track the heading at the viewport top in both modes.
        PreviewScroll.ScrollChanged += OnPreviewScrollChanged;
        // Freeze the preview's width while a resize is in flight (see _resizeSettleTimer).
        PreviewScroll.SizeChanged += OnPreviewViewportSizeChanged;
        Source.TextArea.TextView.ScrollOffsetChanged += OnSourceScrollChanged;
        // The reader's position is sacred: nothing inside the preview may yank the page via
        // bring-into-view (embedded code editors request it on focus/caret/selection against
        // their broken infinite-viewport geometry). All preview navigation goes through
        // explicit PreviewScroll.Offset writes, so swallowing the request loses nothing.
        Preview.AddHandler(RequestBringIntoViewEvent, OnPreviewRequestBringIntoView);
        // Image lightbox (ported): a left-click on a rendered image opens it full-size.
        Preview.AddHandler(PointerPressedEvent, OnPreviewPointerPressed);
        // Code minimap (ported): clicks land the clicked line at the viewport top.
        Minimap.LineRequested += OnMinimapLineRequested;
        // In-place editing (M15): track the unsaved-changes flag (see OnSourceTextChanged).
        Source.TextChanged += OnSourceTextChanged;
        // Find bar (Ctrl+F) lives in this view: Enter / Shift+Enter cycle matches, Esc closes (the
        // central MainWindow dispatcher only opens it).
        SearchBox.KeyDown += OnSearchBoxKeyDown;
        // Editor context menu (#26): grey out «Копировать» while nothing is selected.
        if (Source.ContextFlyout is MenuFlyout editorMenu)
            editorMenu.Opening += OnEditorMenuOpening;
        // cv-* decorations (ported): hover shows the resolved value (decoded entity, byte
        // count, relative date) as a native tooltip; brushes follow the theme.
        Source.TextArea.TextView.PointerHover += OnSourceHover;
        Source.TextArea.TextView.PointerHoverStopped += OnSourcePointerHoverStopped;
        AttachedToVisualTree += OnAttachedRefreshPalette;
        ActualThemeVariantChanged += OnThemeVariantChangedRefresh;

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnViewDetached;
    }

    // --- Constructor-wired handlers (named so each subscription has a matching -=). ---

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_vm is null)
            return;
        var words = Core.Text.TextStatistics.CountWords(Source.SelectedText);
        _vm.SelectionInfo = words > 0 ? $"выдел.: {words} сл." : string.Empty;
    }

    private void OnPreviewRequestBringIntoView(object? sender, RequestBringIntoViewEventArgs e)
        => e.Handled = true;

    private void OnMinimapLineRequested(int line) => ScrollSourceToLine(line);

    // TextLength is O(1) and alloc-free, so the common keystroke never materialises the document
    // string (TextEditor.Text copies the WHOLE buffer — megabytes per keypress on big files). Only
    // the rare equal-length case (char replacement, undo back to loaded) pays the full compare; a
    // programmatic reload compares equal and stays clean.
    private void OnSourceTextChanged(object? sender, EventArgs e)
    {
        if (_vm is null)
            return;
        var loaded = _vm.SourceText ?? string.Empty;
        if ((Source.Document?.TextLength ?? 0) != loaded.Length)
            _vm.IsEdited = true;
        else
            _vm.IsEdited = (Source.Text ?? string.Empty) != loaded;
    }

    private void OnEditorMenuOpening(object? sender, EventArgs e) => RefreshEditorMenu();

    private void OnSourcePointerHoverStopped(object? sender, PointerEventArgs e)
        => ToolTip.SetIsOpen(Source, false);

    private void OnAttachedRefreshPalette(object? sender, VisualTreeAttachmentEventArgs e)
        => RefreshCvPalette();

    private void OnThemeVariantChangedRefresh(object? sender, EventArgs e)
    {
        RefreshCvPalette();
        if (_cvAttached || _indentGuidesAttached)
            Source.TextArea.TextView.Redraw();
    }

    private void OnViewDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Unsubscribe();         // VM events + folding + reflow timer stop
        DetachChildHandlers(); // child-control / self handlers wired once in the constructor
    }

    /// <summary>Mirror of the constructor's child-control/self subscriptions, torn down on final
    /// detach so a closed tab's DocumentView retains no live delegates. VM-event handlers live in
    /// Unsubscribe. Internal flag lets a headless test confirm the teardown ran.</summary>
    private void DetachChildHandlers()
    {
        _previewReflowTimer.Tick -= OnPreviewReflowTick;
        _resizeSettleTimer.Tick -= OnResizeSettleTick;
        Source.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        Source.TextArea.SelectionChanged -= OnSelectionChanged;
        PreviewScroll.ScrollChanged -= OnPreviewScrollChanged;
        PreviewScroll.SizeChanged -= OnPreviewViewportSizeChanged;
        Source.TextArea.TextView.ScrollOffsetChanged -= OnSourceScrollChanged;
        Preview.RemoveHandler(RequestBringIntoViewEvent, OnPreviewRequestBringIntoView);
        Preview.RemoveHandler(PointerPressedEvent, OnPreviewPointerPressed);
        Minimap.LineRequested -= OnMinimapLineRequested;
        Source.TextChanged -= OnSourceTextChanged;
        SearchBox.KeyDown -= OnSearchBoxKeyDown;
        if (Source.ContextFlyout is MenuFlyout editorMenu)
            editorMenu.Opening -= OnEditorMenuOpening;
        Source.TextArea.TextView.PointerHover -= OnSourceHover;
        Source.TextArea.TextView.PointerHoverStopped -= OnSourcePointerHoverStopped;
        AttachedToVisualTree -= OnAttachedRefreshPalette;
        ActualThemeVariantChanged -= OnThemeVariantChangedRefresh;
        ChildHandlersDetached = true;
    }

    internal bool ChildHandlersDetached { get; private set; }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _vm = DataContext as DocumentTabViewModel;
        UpdateCvDecorationPolicy();
        if (_vm is not null)
        {
            _vm.EditorTextProvider = () => Source.Text ?? string.Empty; // M15 save pulls from here
            ApplySourceEditMode(); // read-only while a display transform (pretty-JSON/typography) is on
            _vm.NavigationRequested += OnNavigationRequested;
            _vm.GoToLineRequested += OnGoToLineRequested;
            _vm.SearchUpdated += OnSearchUpdated;
            _vm.FoldAllRequested += OnFoldAllRequested;
            _vm.PropertyChanged += OnVmPropertyChanged;
            // After the new document/layout settles, in ONE dispatcher turn (no ordering between
            // them, so three separate Posts only added interleaved paints — part of the empty-frame
            // cascade): refresh the caret readout and focus a source tab's editor (#29), then fill
            // folding/minimap, which need the bound Document the bindings have just applied.
            Dispatcher.UIThread.Post(() =>
            {
                ActivateSource();
                UpdateSectionFolding();
                RefreshMinimap();
            });

            // A reload's replacement tab carries the old tab's reading position (M14): consume
            // the one-shot anchor and apply it once the fresh view has laid out (Loaded = just after
            // the layout pass, so the content doesn't paint at the top and then jump). The generation
            // guard lets an explicit TOC jump arriving in between win.
            if (_vm.RestoreAnchor is { } restore)
            {
                _vm.RestoreAnchor = null;
                var vm = _vm;
                var gen = ++_syncGeneration;
                Dispatcher.UIThread.Post(() =>
                {
                    if (gen != _syncGeneration || !ReferenceEquals(vm, _vm))
                        return;
                    if (vm.ShowPreview)
                        ApplyToPreview(restore, retryAfterLayout: true);
                    else
                        ApplyToSource(restore);
                }, DispatcherPriority.Loaded);
            }
        }
    }

    // Tabs are kept alive (DataContext is set once), so focus must follow ACTIVATION: re-focus the
    // editor when this tab becomes the active one.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTabViewModel.IsActive) && _vm?.IsActive == true)
            Dispatcher.UIThread.Post(ActivateSource);
        else if (e.PropertyName == nameof(DocumentTabViewModel.IsSearchOpen))
            Dispatcher.UIThread.Post(OnSearchOpenChanged);
        else if (e.PropertyName == nameof(DocumentTabViewModel.ViewMode))
        {
            UnfreezePreviewWidth(); // a mode switch must not carry a stale width pin into the next show
            // R8: only a visible tab needs to re-anchor its scroll now (a full GetVisualDescendants
            // reflow walk). A kept-alive background tab whose ViewMode is mutated (palette path) syncs
            // when it next activates, not while hidden.
            if (_vm?.IsActive == true)
                SyncPositionAcrossModes();
        }
        else if (e.PropertyName == nameof(DocumentTabViewModel.IsSourceTransformActive))
        {
            ApplySourceEditMode();
        }
    }

    // A display transform (pretty-JSON / smart typography) shows text that is NOT the raw file, so
    // editing it would let Save persist the transformed buffer back over the document (lossy —
    // re-indentation, «guillemets», em-dashes). Make the source editor read-only while a transform
    // is active; toggling it off restores the raw text and editing.
    private void ApplySourceEditMode()
        => Source.IsReadOnly = _vm?.IsSourceTransformActive == true;

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

    // First layout: run the heavy passes now so the opened document is complete in one frame.
    // Later reflows (resize/zoom storms): restart the debounce so they collapse into one trailing
    // run instead of one-per-frame. _previewReflowPrimed resets per content in Unsubscribe.
    // H6: a scroll-changed extent delta during a resize must NOT restart the reflow debounce while the
    // preview width is pinned (no re-wrap is happening). The pin's release (UnfreezePreviewWidth → NaN)
    // triggers the real re-layout, whose extent change schedules the single trailing reflow.
    private void MaybeScheduleReflowOnExtentChange()
    {
        if (!_previewFrozen)
            SchedulePreviewReflow();
    }

    private void SchedulePreviewReflow()
    {
        PreviewReflowScheduleCount++;
        if (!_previewReflowPrimed)
        {
            _previewReflowPrimed = true;
            RunPreviewReflowPasses();
        }
        else
        {
            _previewReflowTimer.Stop();
            _previewReflowTimer.Start();
        }
    }

    private void OnPreviewReflowTick(object? sender, EventArgs e)
    {
        _previewReflowTimer.Stop();
        RunPreviewReflowPasses();
    }

    /// <summary>The heavy preview-reflow passes, coalesced: drop+rebuild the heading-Y cache from
    /// the settled layout, re-pin embedded code-editor heights, and (idempotently) wire late-realised
    /// tables/sections. Internal so a headless test can count invocations.</summary>
    private void RunPreviewReflowPasses()
    {
        _previewReflowPassCount++;

        // One traversal of the (whole, non-virtualised) preview tree, bucketed — instead of three
        // separate GetVisualDescendants walks (code editors, tables, headings). This runs on the
        // first layout and on every resize-settle, so collapsing it to a single pass trims the
        // per-reflow overhead at exactly the expensive moments. Snapshot before mutating:
        // FixupEmbeddedCodeEditors re-parents editors, which a lazy walk couldn't survive.
        var editors = new List<AvaloniaEdit.TextEditor>();
        var tables = new List<Grid>();
        var headings = new List<Control>();
        foreach (var visual in Preview.GetVisualDescendants())
        {
            switch (visual)
            {
                case AvaloniaEdit.TextEditor editor:
                    editors.Add(editor);
                    break;
                case Grid grid when grid.Classes.Contains("Table"):
                    tables.Add(grid);
                    break;
                case Control control when IsTopLevelHeading(control):
                    headings.Add(control);
                    break;
            }
        }

        FixupEmbeddedCodeEditors(editors);
        PreviewTableSorter.AttachAll(tables); // ported click-to-sort, idempotent
        PreviewSectionCollapser.AttachAll(Preview); // ported collapsible sections (top-level, idempotent)
        // Warm the heading-Y cache from the SAME pass, AFTER the code-editor heights are pinned
        // (pinning shifts heading positions) — same ordering the lazy path had.
        _previewHeadingTops = ComputePreviewHeadingTops(headings);
        RecomputeActiveHeading(); // marker/breadcrumbs correct against the fresh cache (guards internally)
    }

    /// <summary>If a debounced reflow is pending, run it now — so an explicit navigation (TOC jump,
    /// mode-toggle sync) reads a fresh heading-Y cache instead of stale mid-drag positions.</summary>
    private void FlushPendingPreviewReflow()
    {
        if (_previewReflowTimer.IsEnabled)
        {
            _previewReflowTimer.Stop();
            RunPreviewReflowPasses();
        }
    }

    // Test seams (headless): assert the resize storm coalesces instead of running per frame.
    internal int PreviewReflowPassCount => _previewReflowPassCount;

    // H6 seam: counts scheduling attempts; the simulate-extent seam routes through the real guard.
    internal int PreviewReflowScheduleCount { get; private set; }

    internal bool PreviewReflowPending => _previewReflowTimer.IsEnabled;

    internal void SimulatePreviewExtentChangeForTest() => MaybeScheduleReflowOnExtentChange();

    // --- Resize freeze: pin the preview width while a resize is in flight, release on settle. The
    //     root cost on resize is Markdown.Avalonia re-measuring the whole non-virtualised document on
    //     every width change. An explicit Width caches the measure, so during the drag the document
    //     keeps its layout (0 re-wraps) and re-lays-out exactly once when the drag settles. ---
    private void OnPreviewViewportSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Only a genuine width change re-wraps the document. Skip the initial 0→size layout and
        // height-only changes (vertical resize doesn't reflow) — nothing to coalesce there.
        if (e.PreviousSize.Width <= 0 || e.NewSize.Width == e.PreviousSize.Width)
            return;
        HandlePreviewResize();
    }

    private void HandlePreviewResize()
    {
        if (_vm is not { ShowPreview: true })
            return; // source/notice tabs hide the preview; nothing to freeze

        if (!_previewFrozen)
        {
            var width = Preview.Bounds.Width;
            if (width <= 0)
                return; // not laid out yet — let the first real layout happen
            // Pin at the CURRENT (already reading-width-capped) width: the constraint stops changing,
            // so subsequent resize frames hit the measure cache instead of re-wrapping the document.
            Preview.Width = width;
            _previewFrozen = true;
        }

        _resizeSettleTimer.Stop();
        _resizeSettleTimer.Start();
    }

    private void OnResizeSettleTick(object? sender, EventArgs e)
    {
        _resizeSettleTimer.Stop();
        UnfreezePreviewWidth();
    }

    // Release the pin → the preview re-stretches to the viewport (capped by the reading-width MaxWidth)
    // and re-lays-out once. Safe to call when not frozen (NaN restores auto width).
    private void UnfreezePreviewWidth()
    {
        if (!_previewFrozen)
            return;
        _previewFrozen = false;
        Preview.Width = double.NaN;
    }

    // Test seams (headless): assert the preview width pins during a resize and releases on settle.
    internal bool PreviewWidthFrozen => _previewFrozen;

    internal void SimulatePreviewResizeForTest() => HandlePreviewResize();

    internal void SettlePreviewResizeForTest() => OnResizeSettleTick(null, EventArgs.Empty);

    // Test seams (headless): the rendered preview column's left offset within its (viewport-filling)
    // wrapper and its laid-out width — used to prove the reading-column presets actually CENTER
    // (offset > 0), not just that the converter returns Center (a ScrollViewer would swallow it).
    // The centering lives on the zoom wrapper (LayoutTransformControl), so measure its offset; the
    // logical column width is the MarkdownScrollViewer's Bounds (pre-scale, capped by reading width).
    internal double PreviewColumnLeftOffsetForTest => PreviewZoom.Bounds.X;

    internal double PreviewColumnWidthForTest => Preview.Bounds.Width;

    private void OnBackToTopClick(object? sender, RoutedEventArgs e)
        => PreviewScroll.Offset = PreviewScroll.Offset.WithY(0);

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Preview).Properties.IsLeftButtonPressed)
            return;
        if (TryOpenLightbox(e.Source))
        {
            e.Handled = true;
            return;
        }

        // Checkbox click-to-toggle (M15): only the glyph zone at the line start flips the
        // box — the rest of the item stays selectable text.
        if (e.Source is ColorTextBlock.Avalonia.CTextBlock block
            && TaskGlyphIndexOf(block) is { } taskIndex
            && e.GetPosition(block).X < TaskGlyphZoneWidth
            && _vm is { Shell: { } shell } vm)
        {
            _ = shell.ToggleTaskAsync(vm, taskIndex);
            e.Handled = true;
        }
    }

    /// <summary>Clickable width of the leading ☐/☑ glyph, px.</summary>
    private const double TaskGlyphZoneWidth = 26;

    /// <summary>Index of a task-glyph block among the preview's TOP-LEVEL task-glyph blocks,
    /// in visual (= document) order — the contract <c>TaskListToggle</c> maps back to the raw
    /// text. Admonition-nested glyphs are excluded on BOTH ends: their raw lines carry a
    /// <c>&gt;</c> prefix the toggle regex doesn't match (the callout pass de-quotes them only
    /// for display), so counting them would desync every later index. Null when the block is
    /// not a (toggleable) task item. Internal for headless tests.</summary>
    internal int? TaskGlyphIndexOf(ColorTextBlock.Avalonia.CTextBlock block)
    {
        if (!IsToggleableTaskGlyph(block))
            return null;

        var index = 0;
        foreach (var candidate in Preview.GetVisualDescendants()
                     .OfType<ColorTextBlock.Avalonia.CTextBlock>())
        {
            if (ReferenceEquals(candidate, block))
                return index;
            if (IsToggleableTaskGlyph(candidate))
                index++;
        }

        return null;

        static bool IsToggleableTaskGlyph(ColorTextBlock.Avalonia.CTextBlock candidate)
        {
            var trimmed = candidate.Text?.TrimStart();
            return trimmed is not null && trimmed.Length > 0 && trimmed[0] is '☐' or '☑'
                && !candidate.GetVisualAncestors().OfType<Border>()
                    .Any(b => b.Classes.Contains("admonition"));
        }
    }

    /// <summary>Opens the lightbox when the event source sits inside a rendered image.
    /// Internal so headless tests can probe without synthesizing pointer events.</summary>
    internal bool TryOpenLightbox(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != Preview; visual = visual.GetVisualParent())
        {
            if (visual is Image { Source: { } image })
            {
                if (TopLevel.GetTopLevel(this) is Window owner)
                    ImageLightboxWindow.Open(owner, image);
                return true;
            }
        }

        return false;
    }

    /// <summary>Soft cap so a pathological multi-thousand-line fence can't materialise hundreds
    /// of thousands of pixels of text visuals at once (a capped block stays inner-scrollable).</summary>
    private const double MaxEmbeddedCodeEditorHeight = 50_000;

    /// <summary>Markdown.Avalonia (SyntaxHigh) renders fenced code as embedded AvaloniaEdit
    /// editors, which cannot size themselves under the infinite height our outer-scroll layout
    /// provides: the inner ScrollViewer reports an infinite viewport, its extent reads roughly
    /// DOUBLE the real content (so it can't be trusted either), and every BringCaretToView /
    /// bring-into-view clamps against that broken geometry — long blocks rendered cut off and a
    /// click snapped the page around. The deterministic fix: pin each embedded editor's height
    /// to lineCount × the editor's REAL line height (code blocks never wrap) plus its chrome,
    /// so the block shows everything and the page scroll flows straight through it. Runs on
    /// every preview reflow (extent change); the equality guard keeps it convergent, and a
    /// re-run picks up late font/line-height changes.</summary>
    private void FixupEmbeddedCodeEditors(IReadOnlyList<AvaloniaEdit.TextEditor> editors)
    {
        foreach (var editor in editors)
        {
            EnsureCodeCopyButton(editor);
            if (editor.Document is not { LineCount: > 0 } doc)
                continue;

            // The only honest line height is a MATERIALISED visual line: both DefaultLineHeight
            // and the scroll extent come from the height tree's default-properties estimate,
            // which reads ~double the rendered height here. Phase 1 (no visual lines yet) uses
            // the estimate just to make the viewport finite; once lines exist, phase 2 pins the
            // height from the real rendered line. Code lines never wrap, so
            // lineCount × lineHeight IS the content height.
            var textView = editor.TextArea.TextView;
            double lineHeight;
            if (textView.VisualLinesValid && textView.VisualLines.Count > 0)
                lineHeight = textView.VisualLines[0].Height;
            else if (textView.DefaultLineHeight > 0)
                lineHeight = textView.DefaultLineHeight;
            else
                continue; // font not applied yet — the next reflow pass will get it

            // 8 = editor padding/border chrome, 14 = horizontal scrollbar lane, 2 = slack so
            // the inner viewport never dips below its extent (which would materialise the
            // vertical scrollbar — and shaping its FluentAvalonia glyphs crashes headless).
            var target = Math.Min(doc.LineCount * lineHeight + 8 + 14 + 2, MaxEmbeddedCodeEditorHeight);
            if (Math.Abs((double.IsNaN(editor.Height) ? -1 : editor.Height) - target) > 1)
                editor.Height = target;
        }
    }

    /// <summary>Floats a ghost «copy» button over an embedded fenced-code editor (ported).
    /// SyntaxHigh nests the editor in a CodePad inside the code-block Border; we slip a Grid
    /// between the Border and its child once (the "code-copy-host" class marks a done block).</summary>
    private void EnsureCodeCopyButton(AvaloniaEdit.TextEditor editor)
    {
        // Nearest Border up the logical chain, capped — a miss means an unexpected structure.
        Border? border = null;
        var node = editor.Parent;
        for (var i = 0; i < 3 && node is not null; i++, node = node.Parent)
        {
            if (node is Border b)
            {
                border = b;
                break;
            }
        }

        if (border?.Child is not { } content)
            return;
        if (content is Grid wrapped && wrapped.Classes.Contains("code-copy-host"))
            return; // already wrapped

        var button = new Button
        {
            Content = "⧉",
            FontSize = 13,
            Padding = new Thickness(7, 3),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 22, 0), // clear of the editor's scrollbar lane
            Opacity = 0.55,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        button.Classes.Add("code-copy");
        ToolTip.SetTip(button, "Скопировать код");
        AutomationProperties.SetName(button, "Скопировать код");
        button.Click += (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            _ = PerformCodeCopy(button, clipboard is null ? null : clipboard.SetTextAsync,
                editor.Text ?? string.Empty);
        };

        // Detach first: a child still parented to the Border can't join the Grid.
        var grid = new Grid();
        grid.Classes.Add("code-copy-host");
        border.Child = null;
        grid.Children.Add(content);
        grid.Children.Add(button);
        border.Child = grid;
    }

    // Copy the code to the clipboard and flash a ✓ confirmation. The clipboard write is passed as a
    // delegate (decouples the test from the version-fragile IClipboard surface). R6/Q16: the await is
    // guarded — a clipboard failure must not escape this fire-and-forget call as an unobserved
    // UI-thread exception — and the confirmation swap only runs while the button is still attached
    // (the tab can close in the 1.2 s window).
    internal static async Task PerformCodeCopy(Button button, Func<string, Task>? copy, string text)
    {
        if (copy is null)
            return;
        try
        {
            await copy(text);
        }
        catch
        {
            return; // clipboard busy / denied — swallow, no confirmation
        }

        if (button.GetVisualRoot() is null)
            return; // detached between click and now
        button.Content = "✓";
        DispatcherTimer.RunOnce(() =>
        {
            if (button.GetVisualRoot() is not null)
                button.Content = "⧉";
        }, TimeSpan.FromSeconds(1.2));
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

    // Find bar opened → focus + select the query box; closed → hand focus back to the editor.
    private void OnSearchOpenChanged()
    {
        if (_vm is null)
            return;

        if (_vm.IsSearchOpen)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        else if (_vm.ShowSource)
        {
            Source.TextArea.Focus();
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

    // The tab VM recomputed matches (or navigated): repaint the highlight layer with the themed brushes,
    // then bring the current match into view. Deferred so a markdown preview→source switch is laid out.
    private void OnSearchUpdated()
    {
        if (_vm is null)
            return;

        EnsureSearchRendererAttached();
        var accent = TryBrush("AccentBrush");
        _searchRenderer.Update(_vm.SearchMatches, _vm.SearchCurrentIndex, TryBrush("SearchMatchBrush"),
            accent is null ? null : new Pen(accent, 1.4));
        Source.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);

        var i = _vm.SearchCurrentIndex;
        if (i >= 0 && i < _vm.SearchMatches.Count)
        {
            var offset = _vm.SearchMatches[i].Offset;
            var vm = _vm;
            var gen = ++_syncGeneration;
            Dispatcher.UIThread.Post(() =>
            {
                // R13/Q14: re-check the VM + generation — a tab close/swap in between would otherwise
                // move a foreign editor's caret to this (now stale) match offset.
                if (gen != _syncGeneration || !ReferenceEquals(vm, _vm))
                    return;
                Source.TextArea.Caret.Offset = offset;
                Source.TextArea.Caret.BringCaretToView();
            });
        }
    }

    private void EnsureSearchRendererAttached()
    {
        if (_searchRendererAttached)
            return;
        Source.TextArea.TextView.BackgroundRenderers.Add(_searchRenderer);
        _searchRendererAttached = true;
    }

    private IBrush? TryBrush(string key)
        => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

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
        return CodeDecorations.ScanLine(text, _cvColorizer.Today())
            .FirstOrDefault(d => d.Tooltip is not null && column >= d.Start && column < d.Start + d.Length)
            ?.Tooltip;
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    _vm.PreviousMatchCommand.Execute(null);
                else
                    _vm.NextMatchCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                _vm.CloseSearchCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // Editor context-menu actions (#26) — the editor itself owns clipboard/selection; Найти
    // reuses the tab VM's Ctrl+F seam. Click handlers (not bindings): flyout content only
    // inherits a DataContext once shown, which headless tests can't do.
    private void OnEditorCopyClick(object? sender, RoutedEventArgs e) => Source.Copy();

    private void OnEditorSelectAllClick(object? sender, RoutedEventArgs e) => Source.SelectAll();

    private void OnEditorFindClick(object? sender, RoutedEventArgs e) => _vm?.OpenSearchCommand.Execute(null);

    /// <summary>Sync «Копировать»'s enabled state with the selection — called on flyout Opening;
    /// internal so headless tests can drive it (showing the popup shapes the FluentAvalonia
    /// Symbols font, which crashes headless).</summary>
    internal void RefreshEditorMenu() => EditorCopyItem.IsEnabled = Source.SelectionLength > 0;

    private void OnCaretPositionChanged(object? sender, EventArgs e) => UpdateCaret();

    private void UpdateCaret()
    {
        if (_vm is null)
            return;

        var caret = Source.TextArea.Caret;
        _vm.CaretLine = caret.Line;
        _vm.CaretColumn = caret.Column;
    }

    private void ActivateSource()
    {
        // Only the active (visible) tab may take focus — never steal it to a hidden, kept-alive view.
        if (_vm is null || !_vm.IsActive)
            return;

        UpdateCaret();
        RecomputeActiveHeading();
        if (_vm.ShowSource)
            Source.TextArea.Focus(); // the TextArea handles keyboard, not the TextEditor wrapper
    }

    private void Unsubscribe()
    {
        CancelPendingSync();
        _previewReflowTimer.Stop();
        _previewReflowPrimed = false; // new content re-primes its first reflow immediately
        _resizeSettleTimer.Stop();
        UnfreezePreviewWidth(); // new content must start from auto width, not a stale pin
        if (_vm is not null)
        {
            _vm.EditorTextProvider = null;
            _vm.NavigationRequested -= OnNavigationRequested;
            _vm.GoToLineRequested -= OnGoToLineRequested;
            _vm.SearchUpdated -= OnSearchUpdated;
            _vm.FoldAllRequested -= OnFoldAllRequested;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        if (_foldingManager is not null)
        {
            AvaloniaEdit.Folding.FoldingManager.Uninstall(_foldingManager);
            _foldingManager = null;
        }

        _vm = null;
    }

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
