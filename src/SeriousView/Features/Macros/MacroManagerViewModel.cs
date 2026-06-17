using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Editing;

namespace SeriousView.Features.Macros;

/// <summary>The macro library the manager dialog edits — implemented by the shell view-model. A seam so
/// the manager VM is testable without constructing the heavyweight shell.</summary>
public interface IMacroLibrary
{
    IReadOnlyList<Macro> Macros { get; }

    void ReplaceMacroLibrary(IReadOnlyList<Macro> macros);

    void ReplayMacro(Macro macro);
}

/// <summary>One editable row: a renamable name, a repeat count, and the (immutable) recorded steps.</summary>
public partial class MacroRowViewModel : ObservableObject
{
    public MacroRowViewModel(Macro macro)
    {
        _name = macro.Name;
        _count = macro.Count < 1 ? 1 : macro.Count;
        _shortcut = macro.Shortcut;
        Steps = macro.Steps;
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _count;

    /// <summary>The assigned key gesture (e.g. "Ctrl+Shift+M"), or null when unbound.</summary>
    [ObservableProperty]
    private string? _shortcut;

    /// <summary>True while the dialog is waiting for the user to press a combo for this row.</summary>
    [ObservableProperty]
    private bool _capturing;

    public int StepCount => Steps.Count;

    public IReadOnlyList<IEditorIntent> Steps { get; }

    /// <summary>The capture button's caption: a prompt mid-capture, the gesture when bound, else a hint.</summary>
    public string ShortcutLabel => Capturing ? "нажмите…" : string.IsNullOrEmpty(Shortcut) ? "⌨ задать" : Shortcut;

    /// <summary>Whether the «clear» button shows (a gesture is bound and we're not mid-capture).</summary>
    public bool HasShortcut => !string.IsNullOrEmpty(Shortcut) && !Capturing;

    partial void OnShortcutChanged(string? value) => RefreshShortcutLabels();
    partial void OnCapturingChanged(bool value) => RefreshShortcutLabels();

    private void RefreshShortcutLabels()
    {
        OnPropertyChanged(nameof(ShortcutLabel));
        OnPropertyChanged(nameof(HasShortcut));
    }

    public Macro Build(RepeatMode mode) =>
        new(string.IsNullOrWhiteSpace(Name) ? "Без имени" : Name.Trim(), mode, Count < 1 ? 1 : Count, Steps,
            string.IsNullOrWhiteSpace(Shortcut) ? null : Shortcut);
}

/// <summary>Macro manager dialog VM: rename / delete / run (N times · until EOF) the saved macros. Edits
/// are committed back to the library on delete and when the dialog closes (no separate cancel — apply-on-close).</summary>
public partial class MacroManagerViewModel : ObservableObject
{
    private readonly IMacroLibrary _library;

    public MacroManagerViewModel(IMacroLibrary library)
    {
        _library = library;
        Rows = new ObservableCollection<MacroRowViewModel>(library.Macros.Select(m => new MacroRowViewModel(m)));
    }

    public ObservableCollection<MacroRowViewModel> Rows { get; }

    public bool IsEmpty => Rows.Count == 0;

    [RelayCommand]
    private void Delete(MacroRowViewModel? row)
    {
        if (row is null)
            return;

        Rows.Remove(row);
        OnPropertyChanged(nameof(IsEmpty));
        Commit();
    }

    [RelayCommand]
    private void PlayTimes(MacroRowViewModel? row)
    {
        if (row is not null)
            _library.ReplayMacro(row.Build(RepeatMode.Times));
    }

    [RelayCommand]
    private void PlayToEnd(MacroRowViewModel? row)
    {
        if (row is not null)
            _library.ReplayMacro(row.Build(RepeatMode.UntilNoMatch));
    }

    /// <summary>Write the (renamed / re-counted / re-bound / deleted) rows back to the library. Called on
    /// delete, on a shortcut change, and when the dialog closes.</summary>
    public void Commit() => _library.ReplaceMacroLibrary(Rows.Select(r => r.Build(RepeatMode.Once)).ToList());

    // --- Custom shortcut capture. The VM tracks which row is waiting; the window's key handler reads
    //     IsCapturing and forwards the pressed gesture (or null to cancel) to ApplyCapturedShortcut. ---

    private MacroRowViewModel? _capturingRow;

    /// <summary>Whether a row is waiting for a key combo (the window forwards keys only while true).</summary>
    public bool IsCapturing => _capturingRow is not null;

    /// <summary>Begin capturing a gesture for <paramref name="row"/>; the window completes it on the next key.</summary>
    [RelayCommand]
    private void BeginCapture(MacroRowViewModel? row)
    {
        if (row is null)
            return;
        if (_capturingRow is { } prev)
            prev.Capturing = false; // only one row captures at a time
        _capturingRow = row;
        row.Capturing = true;
    }

    /// <summary>Unbind a row's shortcut and persist.</summary>
    [RelayCommand]
    private void ClearShortcut(MacroRowViewModel? row)
    {
        if (row is null)
            return;
        row.Shortcut = null;
        Commit();
    }

    /// <summary>The window forwards the captured gesture here (null = cancel). Applies it to the capturing
    /// row and persists; a no-op when nothing is capturing.</summary>
    public void ApplyCapturedShortcut(string? gesture)
    {
        if (_capturingRow is not { } row)
            return;

        row.Capturing = false;
        _capturingRow = null;
        if (gesture is null)
            return;

        row.Shortcut = gesture;
        Commit();
    }
}
