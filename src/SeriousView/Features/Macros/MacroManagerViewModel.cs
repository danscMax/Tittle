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
        Steps = macro.Steps;
    }

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _count;

    public int StepCount => Steps.Count;

    public IReadOnlyList<IEditorIntent> Steps { get; }

    public Macro Build(RepeatMode mode) =>
        new(string.IsNullOrWhiteSpace(Name) ? "Без имени" : Name.Trim(), mode, Count < 1 ? 1 : Count, Steps);
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

    /// <summary>Write the (renamed / re-counted / deleted) rows back to the library. Called on delete and
    /// when the dialog closes.</summary>
    public void Commit() => _library.ReplaceMacroLibrary(Rows.Select(r => r.Build(RepeatMode.Once)).ToList());
}
