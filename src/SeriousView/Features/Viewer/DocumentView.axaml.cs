using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    public DocumentView()
    {
        InitializeComponent();

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

        // Relay the editor caret position to the active tab VM (shown in the status bar).
        Source.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        // Selection word count for the status bar (ported).
        Source.TextArea.SelectionChanged += (_, _) =>
        {
            if (_vm is null)
                return;
            var words = Core.Text.TextStatistics.CountWords(Source.SelectedText);
            _vm.SelectionInfo = words > 0 ? $"выдел.: {words} сл." : string.Empty;
        };
        // Scroll-spy (M10): track the heading at the viewport top in both modes.
        PreviewScroll.ScrollChanged += OnPreviewScrollChanged;
        Source.TextArea.TextView.ScrollOffsetChanged += OnSourceScrollChanged;
        // The reader's position is sacred: nothing inside the preview may yank the page via
        // bring-into-view (embedded code editors request it on focus/caret/selection against
        // their broken infinite-viewport geometry). All preview navigation goes through
        // explicit PreviewScroll.Offset writes, so swallowing the request loses nothing.
        Preview.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        // Find bar (Ctrl+F) lives in this view: Enter / Shift+Enter cycle matches, Esc closes (the
        // central MainWindow dispatcher only opens it).
        SearchBox.KeyDown += OnSearchBoxKeyDown;
        // Editor context menu (#26): grey out «Копировать» while nothing is selected.
        if (Source.ContextFlyout is MenuFlyout editorMenu)
            editorMenu.Opening += (_, _) => RefreshEditorMenu();
        // cv-* decorations (ported): hover shows the resolved value (decoded entity, byte
        // count, relative date) as a native tooltip; brushes follow the theme.
        Source.TextArea.TextView.PointerHover += OnSourceHover;
        Source.TextArea.TextView.PointerHoverStopped += (_, _) => ToolTip.SetIsOpen(Source, false);
        AttachedToVisualTree += (_, _) => RefreshCvPalette();
        ActualThemeVariantChanged += (_, _) =>
        {
            RefreshCvPalette();
            if (_cvAttached || _indentGuidesAttached)
                Source.TextArea.TextView.Redraw();
        };

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _vm = DataContext as DocumentTabViewModel;
        UpdateCvDecorationPolicy();
        if (_vm is not null)
        {
            _vm.NavigationRequested += OnNavigationRequested;
            _vm.GoToLineRequested += OnGoToLineRequested;
            _vm.SearchUpdated += OnSearchUpdated;
            _vm.PropertyChanged += OnVmPropertyChanged;
            // After the new document/layout settles: refresh the caret readout and, for a source
            // tab, focus the editor so the keyboard works immediately (#29).
            Dispatcher.UIThread.Post(ActivateSource);

            // A reload's replacement tab carries the old tab's reading position (M14): consume
            // the one-shot anchor and apply it once the fresh view has laid out. The generation
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
                }, DispatcherPriority.Background);
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
            SyncPositionAcrossModes();
    }

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

    private void SyncPositionAcrossModes()
    {
        if (_vm is null || !_vm.IsMarkdown || _vm.ShowNotice)
            return;

        var gen = ++_syncGeneration;
        // Apply AFTER the newly shown view has laid out (Background runs below layout), else
        // the target scroller still reports a stale/zero extent and clamps the offset away.
        if (_vm.ShowSource)
        {
            if (CaptureFromPreview() is { } anchor)
                Dispatcher.UIThread.Post(() =>
                {
                    if (gen == _syncGeneration)
                        ApplyToSource(anchor);
                }, DispatcherPriority.Background);
        }
        else
        {
            var anchor = CaptureFromSource();
            Dispatcher.UIThread.Post(() =>
            {
                if (gen == _syncGeneration)
                    ApplyToPreview(anchor, retryAfterLayout: true);
            }, DispatcherPriority.Background);
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
        // An extent change means reflow (first layout, images, zoom, resize) → cached heading
        // Ys are stale. Offset-only changes keep the cache (content space is scroll-invariant).
        if (e.ExtentDelta.Y != 0)
        {
            InvalidatePreviewHeadingTops();
            FixupEmbeddedCodeEditors();
        }
        if (_vm is { IsActive: true, ShowPreview: true })
        {
            RecomputeActiveHeading();
            if (CaptureFromPreview() is { } anchor)
                _vm.ReadingAnchor = anchor; // live reading position, for reload restore (M14)
        }

        // Back-to-top appears once the reader is a screen below the start (ported).
        BackToTopButton.IsVisible = PreviewScroll.Offset.Y > PreviewScroll.Viewport.Height;
    }

    private void OnBackToTopClick(object? sender, RoutedEventArgs e)
        => PreviewScroll.Offset = PreviewScroll.Offset.WithY(0);

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
    private void FixupEmbeddedCodeEditors()
    {
        // Materialised: EnsureCodeCopyButton re-parents editors, which a lazy walk can't survive.
        foreach (var editor in Preview.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().ToList())
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
        button.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
                return;
            await clipboard.SetTextAsync(editor.Text ?? string.Empty);
            // Quick inline confirmation, then back to the glyph.
            button.Content = "✓";
            DispatcherTimer.RunOnce(() => button.Content = "⧉", TimeSpan.FromSeconds(1.2));
        };

        // Detach first: a child still parented to the Border can't join the Grid.
        var grid = new Grid();
        grid.Classes.Add("code-copy-host");
        border.Child = null;
        grid.Children.Add(content);
        grid.Children.Add(button);
        border.Child = grid;
    }

    private void OnSourceScrollChanged(object? sender, EventArgs e)
    {
        if (_vm is { IsActive: true, ShowSource: true })
        {
            RecomputeActiveHeading();
            _vm.ReadingAnchor = CaptureFromSource();
        }
    }

    /// <summary>Scroll-spy recompute — a binary search over cached positions, cheap enough to
    /// run unthrottled per scroll event. Internal so headless tests can poke it directly.</summary>
    internal void RecomputeActiveHeading()
    {
        if (_vm is null || !_vm.IsMarkdown)
            return;

        if (_vm.ShowPreview)
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
    private void OnGoToLineRequested(int line) => Dispatcher.UIThread.Post(() =>
    {
        ScrollSourceToLine(line);
        Source.TextArea.Focus();
    });

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
            Dispatcher.UIThread.Post(() =>
            {
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
        if (_vm is not null)
        {
            _vm.NavigationRequested -= OnNavigationRequested;
            _vm.GoToLineRequested -= OnGoToLineRequested;
            _vm.SearchUpdated -= OnSearchUpdated;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = null;
    }

    private void OnNavigationRequested(HeadingOutline heading)
    {
        if (_vm is null)
            return;

        CancelPendingSync(); // an explicit jump always beats a pending mode-toggle sync

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

    private void InvalidatePreviewHeadingTops() => _previewHeadingTops = null;

    private IReadOnlyList<double>? EnsurePreviewHeadingTops()
        => _previewHeadingTops ??= ComputePreviewHeadingTops();

    /// <summary>Content-space Y of every preview heading, by walking the rendered tree
    /// (Markdown.Avalonia exposes no scroll/heading API). Scroll-invariant: viewport-relative
    /// TranslatePoint plus the current Offset. Null while the preview hasn't laid out. Index
    /// order matches <see cref="DocumentTabViewModel.Outline"/> — the same contract the M4
    /// navigation has relied on (both walks skip admonition-nested headings).</summary>
    private List<double>? ComputePreviewHeadingTops()
    {
        var offsetY = PreviewScroll.Offset.Y;
        var tops = new List<double>();
        foreach (var heading in Preview.GetVisualDescendants().OfType<Control>().Where(IsTopLevelHeading))
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
