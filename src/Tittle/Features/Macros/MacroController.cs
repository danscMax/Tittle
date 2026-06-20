using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tittle.Core.Abstractions;
using Tittle.Core.Editing;

namespace Tittle.Features.Macros;

/// <summary>Macros (M17): owns the recorder, the saved library and replay wiring — extracted from the
/// shell view-model so the god-VM stays lean and this wiring is testable in isolation. Records the
/// 3-source intent stream (typing, navigation keys, line/EOL commands) and replays through
/// <see cref="MacroReplayEngine"/> + the dispatcher. Persisted to macros.json; played from the manager
/// dialog, the Ctrl+Shift+1..9 slots, or a custom per-macro key gesture.
///
/// Coupling back to the shell is just two closures: <c>getActions</c> resolves the active editor's
/// actions, <c>setStatus</c> writes the status line. The controller references no view-model type.</summary>
public partial class MacroController : ObservableObject, IMacroLibrary
{
    private readonly MacroRecorder _macroRecorder = new();
    private readonly List<Macro> _macros = new();
    private readonly IMacroStore? _macroStore;
    private readonly Func<IEditorActions?> _getActions;
    private readonly Action<string> _setStatus;
    private Macro? _lastMacro;

    public MacroController(IMacroStore? store, Func<IEditorActions?> getActions, Action<string> setStatus)
    {
        _macroStore = store;
        _getActions = getActions;
        _setStatus = setStatus;
        if (_macroStore is not null)
        {
            _macros.AddRange(_macroStore.Load());
            _lastMacro = _macros.Count > 0 ? _macros[^1] : null;
            HasMacro = _macros.Count > 0;
        }
    }

    [ObservableProperty]
    private bool _isRecordingMacro;

    [ObservableProperty]
    private bool _hasMacro;

    /// <summary>Feed an intent to the recorder (the 3 sources call this); a no-op unless recording.</summary>
    public void RecordIntent(IEditorIntent intent) => _macroRecorder.Record(intent);

    /// <summary>IMacroLibrary: the saved macros (read by the manager dialog).</summary>
    public IReadOnlyList<Macro> Macros => _macros;

    /// <summary>IMacroLibrary: replace the library wholesale (rename/delete from the manager) and persist.</summary>
    public void ReplaceMacroLibrary(IReadOnlyList<Macro> macros)
    {
        _macros.Clear();
        _macros.AddRange(macros);
        _lastMacro = _macros.Count > 0 ? _macros[^1] : null;
        HasMacro = _macros.Count > 0;
        _macroStore?.Save(_macros);
    }

    /// <summary>IMacroLibrary: replay a macro (with its own mode) on the active editor.</summary>
    public void ReplayMacro(Macro macro)
    {
        if (_getActions() is { } actions)
            MacroReplayEngine.Replay(macro, intent => EditorCommandDispatcher.Apply(actions, intent));
    }

    /// <summary>Play the Nth saved macro (1-based) — the Ctrl+Shift+1..9 quick-slots.</summary>
    public void PlayMacroBySlot(int oneBasedIndex)
    {
        if (oneBasedIndex >= 1 && oneBasedIndex <= _macros.Count)
            ReplayMacro(_macros[oneBasedIndex - 1]);
    }

    /// <summary>Play the saved macro bound to this key-gesture string (a custom per-macro shortcut), and
    /// return whether one matched. The key tunnel checks built-in shortcuts and the positional
    /// Ctrl+Shift+1..9 slots first, so a custom binding can never shadow them.</summary>
    public bool PlayMacroByGesture(string gesture)
    {
        foreach (var m in _macros)
            if (!string.IsNullOrEmpty(m.Shortcut)
                && string.Equals(m.Shortcut, gesture, StringComparison.OrdinalIgnoreCase))
            {
                ReplayMacro(m);
                return true;
            }

        return false;
    }

    /// <summary>Raised when the user opens the macro manager — the View shows the dialog.</summary>
    public event Action? MacroManagerRequested;

    [RelayCommand]
    private void ShowMacroManager() => MacroManagerRequested?.Invoke();

    /// <summary>Start recording, or stop and keep the captured macro for replay.</summary>
    [RelayCommand]
    private void ToggleMacroRecording()
    {
        if (_macroRecorder.IsRecording)
        {
            var macro = _macroRecorder.Stop($"Макрос {_macros.Count + 1}");
            IsRecordingMacro = false;
            if (macro is not null)
            {
                _macros.Add(macro);
                _lastMacro = macro;
                HasMacro = true;
                _macroStore?.Save(_macros); // persist the library to %AppData%/Tittle/macros.json
                _setStatus($"Макрос «{macro.Name}» записан: {macro.Steps.Count} шаг(ов)");
            }
            else
            {
                _setStatus("Запись отменена — действий не было");
            }
        }
        else
        {
            _macroRecorder.Start();
            IsRecordingMacro = true;
            _setStatus("● Идёт запись макроса…");
        }
    }

    [RelayCommand]
    private void PlayMacro() => RunMacro(RepeatMode.Once);

    [RelayCommand]
    private void PlayMacroToEnd() => RunMacro(RepeatMode.UntilNoMatch);

    private void RunMacro(RepeatMode mode)
    {
        if (_lastMacro is not { } macro || _getActions() is not { } actions)
            return;

        var run = macro with { Mode = mode };
        MacroReplayEngine.Replay(run, intent => EditorCommandDispatcher.Apply(actions, intent));
    }

    /// <summary>Replay a specific saved macro (chosen from the palette) with its own mode.</summary>
    [RelayCommand]
    private void PlaySavedMacro(Macro? macro)
    {
        if (macro is null || _getActions() is not { } actions)
            return;

        MacroReplayEngine.Replay(macro, intent => EditorCommandDispatcher.Apply(actions, intent));
    }

    /// <summary>Remove a saved macro from the library (basic management; rename + a full dialog later).</summary>
    [RelayCommand]
    private void DeleteMacro(Macro? macro)
    {
        if (macro is null || !_macros.Remove(macro))
            return;

        _lastMacro = _macros.Count > 0 ? _macros[^1] : null;
        HasMacro = _macros.Count > 0;
        _macroStore?.Save(_macros);
        _setStatus($"Макрос «{macro.Name}» удалён");
    }
}
