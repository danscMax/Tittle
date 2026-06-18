using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Tittle.Features.Viewer;

// Preview reflow (debounced heavy passes), resize-freeze, embedded code-editor height pinning +
// copy buttons, and the task-glyph cache. Split out of the DocumentView core for cohesion; same class.
public partial class DocumentView
{
    // Debounce for the heavy preview-reflow passes (heading-Y rebuild, embedded code-editor height
    // pinning, sorter/collapser attach). A resize drag raises ScrollChanged every frame; without
    // this they ran 3-4 full visual-tree walks per frame — the resize lag, worst in preview. The
    // first layout runs them immediately (the opened document is complete at once); every later
    // reflow is coalesced into one trailing run. Reset per content (DataContext change).
    private readonly DispatcherTimer _previewReflowTimer;
    private bool _previewReflowPrimed;
    private int _previewReflowPassCount;
    // P4: embedded editors that already carry a copy button. EnsureCodeCopyButton runs every reflow
    // tick; once an editor is wired, skip the 3-parent walk + class check. Reset per content.
    private readonly HashSet<AvaloniaEdit.TextEditor> _copyHostEditors = new();
    // P5: document-ordered toggleable task-glyph blocks, built in the reflow pass that already walks
    // the tree. A checkbox click then indexes into this instead of re-walking. Reset per content.
    private List<ColorTextBlock.Avalonia.CTextBlock>? _taskGlyphs;

    // Resize coalescing for the preview. Markdown.Avalonia does NOT virtualise: the whole document is
    // realised, so Avalonia re-measures every block on EVERY width change — a resize drag re-lays-out
    // the entire document per frame (measured ~68ms/step on a 120-section doc vs ~0.1ms in the
    // virtualised source editor). A control with an explicit Width caches its measure, so while a
    // resize is in flight we PIN Preview.Width (no re-wrap, 0 re-layouts) and release it once on
    // settle (one re-layout). Reset per content in Unsubscribe.
    private readonly DispatcherTimer _resizeSettleTimer;
    private bool _previewFrozen;

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
        var taskGlyphs = new List<ColorTextBlock.Avalonia.CTextBlock>();
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
                case ColorTextBlock.Avalonia.CTextBlock glyph when IsToggleableTaskGlyph(glyph):
                    taskGlyphs.Add(glyph); // P5: cache in document order for click-to-toggle indexing
                    break;
                case Control control when IsTopLevelHeading(control):
                    headings.Add(control);
                    break;
            }
        }

        _taskGlyphs = taskGlyphs;
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

    internal void RunPreviewReflowPassesForTest() => RunPreviewReflowPasses();

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
        if (_vm is null || (!_vm.ShowPreview && !_vm.ShowSplit))
            return; // source/notice tabs hide the preview; nothing to freeze (split DOES show it →
                    // a splitter drag changes the preview width and must coalesce the re-wrap too)

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
        // P5: index into the reflow-built cache instead of re-walking the whole preview tree per click.
        // A lazy rebuild covers a click that lands before any reflow has populated it.
        var glyphs = _taskGlyphs ??= CollectTaskGlyphs();
        var i = glyphs.IndexOf(block);
        return i >= 0 ? i : null;
    }

    // P5 seam: counts the fallback full walks (a cache hit does none).
    internal int TaskGlyphFullWalkCount { get; private set; }

    private List<ColorTextBlock.Avalonia.CTextBlock> CollectTaskGlyphs()
    {
        TaskGlyphFullWalkCount++;
        var list = new List<ColorTextBlock.Avalonia.CTextBlock>();
        foreach (var candidate in Preview.GetVisualDescendants()
                     .OfType<ColorTextBlock.Avalonia.CTextBlock>())
        {
            if (IsToggleableTaskGlyph(candidate))
                list.Add(candidate);
        }

        return list;
    }

    // Admonition-nested glyphs are excluded: their raw lines carry a '>' prefix the toggle regex
    // doesn't match (the callout pass de-quotes them only for display), so counting them would
    // desync every later index against the raw text that TaskListToggle maps back to.
    private static bool IsToggleableTaskGlyph(ColorTextBlock.Avalonia.CTextBlock candidate)
    {
        var trimmed = candidate.Text?.TrimStart();
        return trimmed is { Length: > 0 } && trimmed[0] is '☐' or '☑'
            && !candidate.GetVisualAncestors().OfType<Border>()
                .Any(b => b.Classes.Contains("admonition"));
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
            // Rich TextMate highlighting for preview code blocks (SyntaxHigh's built-in is near-monochrome).
            // The fence language SyntaxHigh stashed in editor.Tag drives the grammar; install BEFORE the
            // height-pin below so the line metrics reflect the highlighted render.
            EditorBehavior.ApplyPreviewGrammar(editor);
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
    // P4 seam: counts how often EnsureCodeCopyButton does the parent walk (past the wired early-out).
    internal int CopyButtonWalkCount { get; private set; }

    private void EnsureCodeCopyButton(AvaloniaEdit.TextEditor editor)
    {
        if (_copyHostEditors.Contains(editor))
            return; // P4: already wired — skip the 3-parent walk on every later reflow tick
        CopyButtonWalkCount++;

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
        _copyHostEditors.Add(editor); // mark only on a successful wrap (a not-yet-laid-out block retries)
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
}
