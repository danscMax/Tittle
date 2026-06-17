using System.Collections.Generic;
using Tittle.Core.Editing;

namespace Tittle.Core.Abstractions;

/// <summary>Loads / saves the user's macro library (a JSON file). Abstracted so the shell view-model
/// stays testable without the filesystem; the implementation serializes through the allowlist-guarded
/// <see cref="MacroSerializer"/>.</summary>
public interface IMacroStore
{
    IReadOnlyList<Macro> Load();

    void Save(IReadOnlyList<Macro> macros);
}
