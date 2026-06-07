using System;
using System.Collections.Generic;

namespace SeriousView.Core.Text;

/// <summary>Pure helpers for identifying markdown documents (UI-free, testable).</summary>
public static class MarkdownFile
{
    // Common markdown file extensions (leading dot, e.g. ".md"). Case-insensitive.
    private static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown", ".mdown", ".mkd", ".markdn" };

    /// <summary>True when <paramref name="extension"/> (e.g. ".md") denotes a markdown file.</summary>
    public static bool IsMarkdownExtension(string? extension)
        => extension is not null && Extensions.Contains(extension);
}
