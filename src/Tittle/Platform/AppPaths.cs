using System;
using System.IO;

namespace Tittle.Platform;

/// <summary>Single source of truth for the per-user app-data folder
/// (<c>%AppData%/Tittle</c> on Windows, <c>~/.config/Tittle</c> on Linux,
/// <c>~/Library/Application Support/Tittle</c> on macOS). Used by the settings store and
/// the crash logger; both build the same folder, so the name lives here once. Pure path math —
/// no I/O, so it is safe to read before DI exists (the crash logger wires up in Program.Main).</summary>
public static class AppPaths
{
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tittle");
}
