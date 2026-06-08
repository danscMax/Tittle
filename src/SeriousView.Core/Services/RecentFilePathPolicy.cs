using System;
using System.IO;

namespace SeriousView.Core.Services;

/// <summary>
/// Pure path policy for the recent-files list: decides whether a path lives under the OS temp folder
/// (e.g. a file opened from an archive or email attachment, which the OS copies into %Temp%). Such
/// throwaway paths should never linger in "Recent". UI-free and unit-tested; the temp root is passed
/// in (rather than read from the environment) so the decision stays deterministic and testable.
/// </summary>
public static class RecentFilePathPolicy
{
    /// <summary>True when <paramref name="path"/> is the temp root itself or sits anywhere under it.
    /// Both sides are normalized to full paths; the compare is case-insensitive (safe across the
    /// supported desktop OSes — a same-name different-case sibling under temp is a non-issue here).
    /// Never throws: a malformed stored path is treated as not-temp.</summary>
    public static bool IsUnderTempFolder(string? path, string? tempRoot)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(tempRoot))
            return false;

        string fullPath, root;
        try
        {
            fullPath = Path.GetFullPath(path);
            root = Path.GetFullPath(tempRoot);
        }
        catch
        {
            return false;
        }

        root = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root.Length == 0)
            return false;

        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            return true;

        // Separator-terminated prefix so "…/Temp" does NOT match a "…/Temp2/…" sibling.
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
