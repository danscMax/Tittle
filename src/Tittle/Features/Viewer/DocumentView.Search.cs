using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;

namespace Tittle.Features.Viewer;

// In-document find bar (Ctrl+F): the highlight renderer, themed repaint + bring-current-into-view,
// and the find-box key handling. Split out of the DocumentView core; same class.
public partial class DocumentView
{
    private readonly SearchHighlightRenderer _searchRenderer = new();
    private bool _searchRendererAttached;

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
}
