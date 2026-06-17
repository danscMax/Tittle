using SeriousView.Core.Editing;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class EditorCommandDispatcherTests
{
    private static string Apply(string text, LineOp op, int selStart = 0, int selLength = 0)
    {
        var editor = new FakeEditorActions(text, selStart, selLength);
        EditorCommandDispatcher.Apply(editor, new TransformLinesIntent(op));
        return editor.Text;
    }

    [Fact]
    public void Sort_NoSelection_SortsWholeDocument()
        => Assert.Equal("apple\nbanana\ncherry", Apply("banana\napple\ncherry", LineOp.SortAscending));

    [Fact]
    public void Sort_WithSelection_OnlySortsTheSelectedLines()
        // "z\nb\na\nx": select "b\na" (offsets 2..4) → only those two lines sort; z and x stay put.
        => Assert.Equal("z\na\nb\nx", Apply("z\nb\na\nx", LineOp.SortAscending, selStart: 2, selLength: 3));

    [Fact]
    public void Upper_WholeDocument()
        => Assert.Equal("ABC\nDEF", Apply("abc\ndef", LineOp.Upper));

    [Fact]
    public void RemoveDuplicates_WholeDocument()
        => Assert.Equal("a\nb\nc", Apply("a\nb\na\nc", LineOp.RemoveDuplicates));

    [Fact]
    public void MoveDown_OnCaretLine_SwapsWithNext()
        => Assert.Equal("b\na\nc", Apply("a\nb\nc", LineOp.MoveDown, selStart: 0));

    [Fact]
    public void MoveUp_OnCaretLine_SwapsWithPrevious()
        // caret on line 2 ("c", offset 4) → moves above "b"
        => Assert.Equal("a\nc\nb", Apply("a\nb\nc", LineOp.MoveUp, selStart: 4));

    [Fact]
    public void Duplicate_CaretLine()
        => Assert.Equal("a\na\nb", Apply("a\nb", LineOp.Duplicate, selStart: 0));

    [Fact]
    public void Join_SelectedLines()
        // select all three lines → joined into one
        => Assert.Equal("a b c", Apply("a\nb\nc", LineOp.Join, selStart: 0, selLength: 5));

    [Theory]
    [InlineData(Eol.CrLf, "a\r\nb\r\nc")]
    [InlineData(Eol.Cr, "a\rb\rc")]
    [InlineData(Eol.Lf, "a\nb\nc")]
    public void ConvertEol_RewritesLineEndings(Eol target, string expected)
    {
        var editor = new FakeEditorActions("a\nb\nc");
        EditorCommandDispatcher.Apply(editor, new ConvertEolIntent(target));
        Assert.Equal(expected, editor.Text);
    }

    [Fact]
    public void NoEffectiveChange_DoesNotCallReplace()
    {
        var editor = new FakeEditorActions("a\nb\nc"); // already sorted
        EditorCommandDispatcher.Apply(editor, new TransformLinesIntent(LineOp.SortAscending));
        Assert.Equal(0, editor.ReplaceCalls);
    }
}
