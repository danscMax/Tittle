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
/// to a heading when the outline raises a navigation request.
/// <para>The implementation is split across partial files by concern: this core file owns the
/// constructor wiring, lifecycle/teardown, the DataContext↔VM binding and the caret readout;
/// <c>DocumentView.Reflow.cs</c> (preview reflow + resize freeze + embedded-editor fixup + task
/// glyphs), <c>DocumentView.ScrollSync.cs</c> (scroll-spy, position sync, navigation),
/// <c>DocumentView.Search.cs</c> (find bar), <c>DocumentView.Decorations.cs</c> (cv-* decorations,
/// minimap, folding) and <c>DocumentView.Interaction.cs</c> (pointer/lightbox/context menu).</para></summary>
public partial class DocumentView : UserControl
{
    private DocumentTabViewModel? _vm;

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
        // named handlers so each += has a matching -=. The handler bodies live in the concern-named
        // partial files (Interaction/ScrollSync/Search/Decorations).
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

    // Shared themed-brush lookup against the current variant (used by the search highlight and the
    // cv-decoration palette).
    private IBrush? TryBrush(string key)
        => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

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
        _copyHostEditors.Clear(); // new content rebuilds the preview tree — re-wire copy buttons
        _taskGlyphs = null; // new content rebuilds the glyph cache on the next reflow/click
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
}
