using System.IO;

namespace Tittle.Core.Text;

/// <summary>
/// Formats a file path for "recent files" display (the ☰ File ▸ Recent submenu and the welcome
/// screen): the file name plus its parent folder, instead of a raw — often temp — full path.
/// Pure; degrades safely on empty/odd input. Existence is NOT checked here (see RecentFilesStore).
/// </summary>
public static class RecentFileLabel
{
    /// <summary>Splits <paramref name="path"/> into (file name, parent folder) for display.</summary>
    public static (string Name, string Folder) Describe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (string.Empty, string.Empty);

        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) // path ended in a separator — use the last real segment
            name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return (name, Path.GetDirectoryName(path) ?? string.Empty);
    }
}
