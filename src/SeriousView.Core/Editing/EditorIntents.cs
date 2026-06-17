using SeriousView.Core.Text;

namespace SeriousView.Core.Editing;

/// <summary>Marker for an editor command-intent — a semantic editor operation the dispatcher applies
/// (and the Phase-2 macro engine records/replays by observing/re-dispatching). Intents are EDITOR-ONLY
/// by design: there is no intent for filesystem / process / network actions, so a saved or shared macro
/// can never be a code-execution vector (the Scintilla guarantee — only editor messages).</summary>
public interface IEditorIntent
{
}

/// <summary>A Notepad++-style line operation. Sort / dedup / trim / case act on the selected lines (or
/// the whole document when nothing is selected); move / duplicate / join act on the touched line range.</summary>
public enum LineOp
{
    SortAscending,
    SortDescending,
    RemoveDuplicates,
    TrimTrailing,
    Upper,
    Lower,
    Title,
    MoveUp,
    MoveDown,
    Duplicate,
    Join,
}

/// <summary>Apply a <see cref="LineOp"/> to the editor's selection (or whole document).</summary>
public sealed record TransformLinesIntent(LineOp Op) : IEditorIntent;

/// <summary>Convert the whole document's line endings to a target style (LF / CRLF / CR). Recordable —
/// it is a text transform; the editor buffer holds the result and a save writes it.</summary>
public sealed record ConvertEolIntent(Eol Target) : IEditorIntent;

/// <summary>Caret movements a macro can record/replay. Computed from the document text + offset (logical
/// lines), so replay is deterministic; Up/Down preserve the column on the target logical line.</summary>
public enum CaretMotion
{
    Left,
    Right,
    Up,
    Down,
    LineStart,
    LineEnd,
    WordLeft,
    WordRight,
    DocStart,
    DocEnd,
}

/// <summary>Insert text at the caret (replacing any selection) — captures typing during recording.</summary>
public sealed record InsertTextIntent(string Text) : IEditorIntent;

/// <summary>Delete one character or the selection: <c>Forward</c> = the Delete key, else Backspace.</summary>
public sealed record DeleteTextIntent(bool Forward) : IEditorIntent;

/// <summary>Move the caret (collapsing any selection).</summary>
public sealed record MoveCaretIntent(CaretMotion Motion) : IEditorIntent;

/// <summary>Find the next match at/after the caret and select it. Returns no-progress at end-of-file (it
/// does NOT wrap) — that is what ends an until-no-match replay.</summary>
public sealed record FindNextIntent(string Pattern, bool Regex, bool CaseSensitive) : IEditorIntent;

/// <summary>Replace the current selection with text — the find-driven-edit partner of FindNext.</summary>
public sealed record ReplaceSelectionIntent(string Text) : IEditorIntent;
