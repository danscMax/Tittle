using System.Collections.Generic;

namespace Tittle.Core.Editing;

/// <summary>Captures editor intents while recording, then assembles a <see cref="Macro"/>. Pure of UI:
/// the app feeds it intents from the 3-source tap (typing → InsertText, navigation/deletion keys →
/// MoveCaret/DeleteText, high-level commands → their own intents). It never records during replay
/// (replay dispatches straight to the engine, and <see cref="IsRecording"/> is false then anyway).</summary>
public sealed class MacroRecorder
{
    private readonly List<IEditorIntent> _steps = new();

    public bool IsRecording { get; private set; }

    public int StepCount => _steps.Count;

    public void Start()
    {
        _steps.Clear();
        IsRecording = true;
    }

    /// <summary>Append an intent while recording. Consecutive single-run text inserts are coalesced into
    /// one <see cref="InsertTextIntent"/> so a typed run replays as one edit, not one edit per keystroke
    /// (a non-insert intent in between breaks the run).</summary>
    public void Record(IEditorIntent intent)
    {
        if (!IsRecording)
            return;

        if (intent is InsertTextIntent ins && _steps.Count > 0 && _steps[^1] is InsertTextIntent prev)
            _steps[^1] = new InsertTextIntent(prev.Text + ins.Text);
        else
            _steps.Add(intent);
    }

    /// <summary>Stop recording and return the captured macro, or <c>null</c> if nothing was recorded.</summary>
    public Macro? Stop(string name, RepeatMode mode = RepeatMode.Once, int count = 1)
    {
        IsRecording = false;
        return _steps.Count == 0 ? null : new Macro(name, mode, count, _steps.ToArray());
    }
}
