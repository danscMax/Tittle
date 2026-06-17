using System;
using System.Collections.Generic;
using System.IO;
using Tittle.Core.Abstractions;
using Tittle.Core.Editing;

namespace Tittle.Platform;

/// <summary>File-backed macro library at <c>%AppData%/Tittle/macros.json</c> (via
/// <see cref="AppPaths"/>), serialized through the allowlist-guarded <see cref="MacroSerializer"/>.
/// A missing or corrupt file loads as an empty library; the save is a temp-then-swap (crash-safe).</summary>
public sealed class MacroStore : IMacroStore
{
    private readonly string _path;

    public MacroStore() : this(Path.Combine(AppPaths.DataDir, "macros.json")) { }

    // Test seam: an explicit file path (the default targets %AppData%/Tittle/macros.json).
    public MacroStore(string path) => _path = path;

    public IReadOnlyList<Macro> Load()
    {
        try
        {
            return File.Exists(_path)
                ? MacroSerializer.Deserialize(File.ReadAllText(_path))
                : Array.Empty<Macro>();
        }
        catch (IOException)
        {
            return Array.Empty<Macro>();
        }
    }

    public void Save(IReadOnlyList<Macro> macros)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, MacroSerializer.Serialize(macros));
        if (File.Exists(_path))
            File.Replace(temp, _path, null);
        else
            File.Move(temp, _path);
    }
}
