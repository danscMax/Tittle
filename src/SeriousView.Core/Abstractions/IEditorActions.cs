namespace SeriousView.Core.Abstractions;

/// <summary>The editor surface the command-intent dispatcher acts on — a minimal seam over the live
/// editor (AvaloniaEdit), so the dispatcher and its tests stay UI-free. Implemented in the UI over a
/// <c>TextEditor</c>; faked in tests. Grows by one member per feature, never speculatively.</summary>
public interface IEditorActions
{
    /// <summary>The whole document text (LF-normalized, as the loader produces).</summary>
    string Text { get; }

    /// <summary>The current selection as (start offset, length); a bare caret is (offset, 0).</summary>
    (int Start, int Length) Selection { get; }

    /// <summary>Replace <paramref name="length"/> chars at <paramref name="start"/> with
    /// <paramref name="newText"/> (one undo step), then select the inserted text.</summary>
    void Replace(int start, int length, string newText);
}
