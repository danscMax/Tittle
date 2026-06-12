using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SeriousView.Features.Viewer;

// Pointer interaction: selection word-count, the bring-into-view swallow, minimap-line + hover relays,
// unsaved-edit tracking, image lightbox, checkbox click-to-toggle, back-to-top, and the editor context
// menu. Split out of the DocumentView core; same class. Handlers here are wired in the constructor.
public partial class DocumentView
{
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
}
