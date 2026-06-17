using SeriousView.Core.Editing;
using Xunit;

namespace SeriousView.Tests.Core;

public class MacroIntentDispatchTests
{
    private static FakeEditorActions At(string text, int caret) => new(text, caret, 0);

    [Fact]
    public void InsertText_AtCaret()
    {
        var ed = At("ac", 1);
        Assert.True(EditorCommandDispatcher.Apply(ed, new InsertTextIntent("b")));
        Assert.Equal("abc", ed.Text);
    }

    [Fact]
    public void InsertText_ReplacesSelection()
    {
        var ed = new FakeEditorActions("axc", 1, 1); // "x" selected
        EditorCommandDispatcher.Apply(ed, new InsertTextIntent("b"));
        Assert.Equal("abc", ed.Text);
    }

    [Fact]
    public void Delete_Backspace_RemovesCharBeforeCaret()
    {
        var ed = At("abc", 2);
        Assert.True(EditorCommandDispatcher.Apply(ed, new DeleteTextIntent(Forward: false)));
        Assert.Equal("ac", ed.Text);
    }

    [Fact]
    public void Delete_Forward_RemovesCharAtCaret()
    {
        var ed = At("abc", 1);
        EditorCommandDispatcher.Apply(ed, new DeleteTextIntent(Forward: true));
        Assert.Equal("ac", ed.Text);
    }

    [Fact]
    public void Delete_Backspace_AtStart_IsNoProgress()
    {
        var ed = At("abc", 0);
        Assert.False(EditorCommandDispatcher.Apply(ed, new DeleteTextIntent(Forward: false)));
        Assert.Equal("abc", ed.Text);
    }

    [Theory]
    [InlineData(CaretMotion.Left, 5, 4)]
    [InlineData(CaretMotion.Right, 5, 6)]
    [InlineData(CaretMotion.LineStart, 5, 4)]
    [InlineData(CaretMotion.LineEnd, 5, 7)]
    [InlineData(CaretMotion.DocStart, 5, 0)]
    [InlineData(CaretMotion.DocEnd, 5, 11)]
    [InlineData(CaretMotion.WordRight, 0, 3)]
    [InlineData(CaretMotion.WordLeft, 3, 0)]
    [InlineData(CaretMotion.Up, 5, 1)]   // line "def" col 1 → line "abc" col 1
    [InlineData(CaretMotion.Down, 5, 9)] // line "def" col 1 → line "ghi" col 1
    public void MoveCaret_ComputesTarget(CaretMotion motion, int from, int expected)
    {
        var ed = new FakeEditorActions("abc\ndef\nghi", from, 0);
        EditorCommandDispatcher.Apply(ed, new MoveCaretIntent(motion));
        Assert.Equal((expected, 0), ed.Selection);
    }

    [Fact]
    public void FindNext_SelectsNextMatchAfterCaret()
    {
        var ed = new FakeEditorActions("a x a x a", 1, 0);
        Assert.True(EditorCommandDispatcher.Apply(ed, new FindNextIntent("a", Regex: false, CaseSensitive: false)));
        Assert.Equal((4, 1), ed.Selection); // the next 'a'
    }

    [Fact]
    public void FindNext_AtEof_ReturnsFalse_DoesNotWrap()
    {
        var ed = new FakeEditorActions("a x a", 5, 0); // caret past both matches
        Assert.False(EditorCommandDispatcher.Apply(ed, new FindNextIntent("a", Regex: false, CaseSensitive: false)));
    }

    [Fact]
    public void ReplaceSelection_ReplacesSelectedText()
    {
        var ed = new FakeEditorActions("axc", 1, 1);
        EditorCommandDispatcher.Apply(ed, new ReplaceSelectionIntent("BB"));
        Assert.Equal("aBBc", ed.Text);
    }

    [Fact]
    public void Replay_FindReplace_UntilEof_TransformsWholeDocument()
    {
        // The killer Notepad++ macro: find each "a" and replace it, run until the find reaches EOF.
        var ed = new FakeEditorActions("a a a", 0, 0);
        var macro = new Macro("r", RepeatMode.UntilNoMatch, 0, new IEditorIntent[]
        {
            new FindNextIntent("a", Regex: false, CaseSensitive: false),
            new ReplaceSelectionIntent("b"),
        });

        MacroReplayEngine.Replay(macro, intent => EditorCommandDispatcher.Apply(ed, intent));

        Assert.Equal("b b b", ed.Text);
    }
}
