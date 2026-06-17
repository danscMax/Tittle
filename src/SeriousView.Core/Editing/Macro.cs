using System.Collections.Generic;

namespace SeriousView.Core.Editing;

/// <summary>How a macro replays: once, a fixed number of times, or until a step stops making progress
/// (a find that no longer matches — the Notepad++/Emacs "run to end of file" model).</summary>
public enum RepeatMode
{
    Once,
    Times,
    UntilNoMatch,
}

/// <summary>A recorded macro: an ordered list of editor intents plus how to replay them. Pure data —
/// the recorder appends <see cref="Steps"/>, the player feeds them to <see cref="MacroReplayEngine"/>.
/// Steps are EDITOR-ONLY intents (see <see cref="IEditorIntent"/>), so a saved macro can never carry a
/// filesystem/process action. <see cref="Shortcut"/> is an optional user-assigned key gesture string
/// (e.g. "Ctrl+Shift+M", in <c>KeyGesture.ToString()</c> form) that plays the macro; null = unbound.</summary>
public sealed record Macro(
    string Name, RepeatMode Mode, int Count, IReadOnlyList<IEditorIntent> Steps, string? Shortcut = null);
