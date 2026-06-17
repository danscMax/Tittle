using Avalonia.Input;
using SeriousView.Core.Editing;
using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

// MainWindow.MacroKeyIntent maps the keys that edit the document WITHOUT raising TextEntered (caret
// nav, deletion, Tab) to recordable intents. Pure logic, so a plain [Fact] suffices.
public class MacroKeyIntentTests
{
    [Fact]
    public void Tab_CollapsedCaret_RecordsTabInsert()
        => Assert.Equal(new InsertTextIntent("\t"),
            MainWindow.MacroKeyIntent(ctrl: false, shift: false, alt: false, Key.Tab, selectionLength: 0));

    // With a selection, AvaloniaEdit block-indents — an InsertText would wrongly replace the selection,
    // so Tab is not recorded in that case.
    [Fact]
    public void Tab_WithSelection_NotRecorded()
        => Assert.Null(MainWindow.MacroKeyIntent(false, false, false, Key.Tab, selectionLength: 5));

    // Ctrl+Tab is the next-tab shortcut, not an edit.
    [Fact]
    public void CtrlTab_NotRecorded()
        => Assert.Null(MainWindow.MacroKeyIntent(ctrl: true, false, false, Key.Tab, 0));

    // Enter raises TextEntered and is recorded by the typing tap; recording it here too would double it.
    [Fact]
    public void Enter_NotRecordedHere()
        => Assert.Null(MainWindow.MacroKeyIntent(false, false, false, Key.Enter, 0));

    [Fact]
    public void PlainLeft_RecordsCaretMove()
        => Assert.Equal(new MoveCaretIntent(CaretMotion.Left),
            MainWindow.MacroKeyIntent(false, false, false, Key.Left, 0));

    [Fact]
    public void CtrlRight_RecordsWordMove()
        => Assert.Equal(new MoveCaretIntent(CaretMotion.WordRight),
            MainWindow.MacroKeyIntent(ctrl: true, false, false, Key.Right, 0));

    [Fact]
    public void Backspace_RecordsBackwardDelete()
        => Assert.Equal(new DeleteTextIntent(Forward: false),
            MainWindow.MacroKeyIntent(false, false, false, Key.Back, 0));

    // Shift-extend (and Alt, which drives column selection) are deferred — not recorded.
    [Fact]
    public void ShiftModifier_NotRecorded()
        => Assert.Null(MainWindow.MacroKeyIntent(false, shift: true, false, Key.Left, 0));

    [Fact]
    public void AltModifier_NotRecorded()
        => Assert.Null(MainWindow.MacroKeyIntent(false, false, alt: true, Key.Right, 0));
}
