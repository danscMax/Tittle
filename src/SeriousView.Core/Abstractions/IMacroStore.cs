using System.Collections.Generic;
using SeriousView.Core.Editing;

namespace SeriousView.Core.Abstractions;

/// <summary>Loads / saves the user's macro library (a JSON file). Abstracted so the shell view-model
/// stays testable without the filesystem; the implementation serializes through the allowlist-guarded
/// <see cref="MacroSerializer"/>.</summary>
public interface IMacroStore
{
    IReadOnlyList<Macro> Load();

    void Save(IReadOnlyList<Macro> macros);
}
