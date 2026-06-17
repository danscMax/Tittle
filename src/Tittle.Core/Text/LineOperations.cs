using System;
using System.Collections.Generic;
using System.Linq;

namespace Tittle.Core.Text;

/// <summary>Which case transform <see cref="LineOperations.ChangeCase"/> applies.</summary>
public enum CaseKind
{
    Upper,
    Lower,
    Title,
}

/// <summary>Pure line-block transforms behind the editor's Notepad++-style line operations
/// (sort / dedup / trim / case / move / duplicate / join). Input and output are a block of lines
/// separated by <c>'\n'</c> — the document is LF-normalized by the loader and the caller passes the
/// exact lines (no trailing newline), so there is no CRLF or trailing-empty handling here. UI-free
/// and testable; the editor dispatcher feeds it the selection (or the whole document) and writes the
/// result back.</summary>
public static class LineOperations
{
    /// <summary>Sort the lines. Case-insensitive (ordinal) by default; <paramref name="descending"/>
    /// reverses the order.</summary>
    public static string Sort(string text, bool descending = false, bool caseSensitive = false)
    {
        var lines = text.Split('\n');
        Array.Sort(lines, caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
        if (descending)
            Array.Reverse(lines);
        return string.Join('\n', lines);
    }

    /// <summary>Drop duplicate lines, keeping the first occurrence and preserving order (exact,
    /// ordinal match).</summary>
    public static string RemoveDuplicateLines(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var kept = new List<string>();
        foreach (var line in text.Split('\n'))
            if (seen.Add(line))
                kept.Add(line);
        return string.Join('\n', kept);
    }

    /// <summary>Trim trailing whitespace from every line.</summary>
    public static string TrimTrailing(string text)
        => string.Join('\n', text.Split('\n').Select(static l => l.TrimEnd()));

    /// <summary>Upper / lower / title case the whole block. Title case upper-cases the first letter of
    /// each word and lower-cases the rest; word boundaries are any non-letter run (works for Cyrillic
    /// and Latin via invariant casing).</summary>
    public static string ChangeCase(string text, CaseKind kind) => kind switch
    {
        CaseKind.Upper => text.ToUpperInvariant(),
        CaseKind.Lower => text.ToLowerInvariant(),
        CaseKind.Title => ToTitleCase(text),
        _ => text,
    };

    private static string ToTitleCase(string text)
    {
        var chars = text.ToCharArray();
        var atWordStart = true;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (char.IsLetter(c))
            {
                chars[i] = atWordStart ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
                atWordStart = false;
            }
            else
            {
                atWordStart = true;
            }
        }

        return new string(chars);
    }

    /// <summary>Move the inclusive 0-based line range <paramref name="startLine"/>..<paramref name="endLine"/>
    /// by <paramref name="delta"/> lines (−1 = up, +1 = down). No-op (returns the input) if the range is
    /// invalid or the move would cross the document edge.</summary>
    public static string MoveLines(string text, int startLine, int endLine, int delta)
    {
        var lines = text.Split('\n').ToList();
        if (delta == 0 || !ValidRange(lines.Count, startLine, endLine))
            return text;
        if (startLine + delta < 0 || endLine + delta >= lines.Count)
            return text; // would move past the first / last line

        var count = endLine - startLine + 1;
        var block = lines.GetRange(startLine, count);
        lines.RemoveRange(startLine, count);
        lines.InsertRange(startLine + delta, block);
        return string.Join('\n', lines);
    }

    /// <summary>Duplicate the inclusive line range, inserting the copy immediately after it.</summary>
    public static string DuplicateLines(string text, int startLine, int endLine)
    {
        var lines = text.Split('\n').ToList();
        if (!ValidRange(lines.Count, startLine, endLine))
            return text;

        var block = lines.GetRange(startLine, endLine - startLine + 1);
        lines.InsertRange(endLine + 1, block);
        return string.Join('\n', lines);
    }

    /// <summary>Join the inclusive line range into one space-separated line (each part trimmed).
    /// Needs at least two lines; otherwise a no-op.</summary>
    public static string JoinLines(string text, int startLine, int endLine)
    {
        var lines = text.Split('\n').ToList();
        if (startLine < 0 || endLine >= lines.Count || startLine >= endLine)
            return text;

        var count = endLine - startLine + 1;
        var joined = string.Join(' ', lines.GetRange(startLine, count).Select(static l => l.Trim()));
        lines.RemoveRange(startLine, count);
        lines.Insert(startLine, joined);
        return string.Join('\n', lines);
    }

    private static bool ValidRange(int lineCount, int startLine, int endLine)
        => startLine >= 0 && endLine < lineCount && startLine <= endLine;
}
