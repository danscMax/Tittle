using System;
using System.IO;

namespace SeriousView.Core.Services;

/// <summary>
/// Pure file-path equality: decides whether two paths point at the same file. Used to reuse an
/// already-open tab when a file is reopened (Ctrl+O, recent, drag-drop, single-instance forwarding).
/// Both sides are normalized to a full path and compared case-insensitively — consistent with the
/// recent-files services and safe across the supported desktop OSes. UI-free and unit-tested; never
/// throws — a malformed path falls back to an ordinal compare of the raw strings.
/// </summary>
public static class FilePathEquality
{
    /// <summary>True when <paramref name="a"/> and <paramref name="b"/> resolve to the same file.
    /// A null/empty path is "no file" and never matches (not even another null) — a tab without a
    /// backing file (the sample) is therefore never reused.</summary>
    public static bool SameFile(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // A malformed path can't be normalized — compare the raw strings rather than throw.
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
