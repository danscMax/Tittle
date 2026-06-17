using AvaloniaEdit;
using Tittle.Core.Abstractions;

namespace Tittle.Features.Viewer;

/// <summary><see cref="IEditorActions"/> over a live AvaloniaEdit <see cref="TextEditor"/> — the bridge
/// the command-intent dispatcher uses to read/mutate the editor. Created per attached
/// <c>DocumentView</c> and handed to the tab view-model (mirroring the <c>EditorTextProvider</c> seam).</summary>
internal sealed class AvaloniaEditorActions(TextEditor editor) : IEditorActions
{
    public string Text => editor.Document?.Text ?? string.Empty;

    public (int Start, int Length) Selection => (editor.SelectionStart, editor.SelectionLength);

    public void Replace(int start, int length, string newText)
    {
        var document = editor.Document;
        if (document is null)
            return;

        document.Replace(start, length, newText); // a single Replace is already one undo step
        SetSelection(start, newText.Length);
    }

    public void SetSelection(int start, int length)
    {
        editor.SelectionStart = start;
        editor.SelectionLength = length;
        editor.CaretOffset = start + length;
    }
}
