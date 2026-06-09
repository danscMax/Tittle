using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

public class EditorBehaviorTests
{
    // Opening a file should show line 1 — assigning the document used to leave the caret at the end,
    // so the status bar read the last line and the first ↓ jumped to the bottom.
    [AvaloniaFact]
    public void SetText_LeavesCaretAtStart()
    {
        var editor = new TextEditor();

        EditorBehavior.SetText(editor, "one\ntwo\nthree");

        Assert.Equal(0, editor.CaretOffset);
    }
}
