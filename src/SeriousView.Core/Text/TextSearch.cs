using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>A search hit: a 0-based <see cref="Offset"/> into the text and its <see cref="Length"/>.</summary>
public readonly record struct MatchRange(int Offset, int Length);

/// <summary>Result of a search: the ordered, non-overlapping <see cref="Matches"/>, and whether the
/// query compiled — <see cref="PatternValid"/> is false only for an invalid regex (the find bar paints
/// the regex toggle red). Literal queries are always valid.</summary>
public readonly record struct SearchOutcome(IReadOnlyList<MatchRange> Matches, bool PatternValid);

/// <summary>
/// Finds all occurrences of a query in text — literal or regex, case-insensitive by default. Pure and
/// UI-free (testable). Drives the in-document find bar; the editor highlights the returned ranges and
/// navigates between them. Zero-width matches (e.g. <c>a*</c>, <c>^</c>) are skipped — nothing to show.
/// </summary>
public static class TextSearch
{
    private static readonly SearchOutcome Empty = new(Array.Empty<MatchRange>(), PatternValid: true);

    /// <summary>Find every match of <paramref name="query"/> in <paramref name="text"/>, in document
    /// order and non-overlapping. Empty text/query yields no matches. An invalid regex yields no matches
    /// with <see cref="SearchOutcome.PatternValid"/> = false.</summary>
    public static SearchOutcome FindAll(string? text, string? query, bool caseSensitive = false, bool regex = false)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
            return Empty;

        return regex
            ? FindRegex(text, query, caseSensitive)
            : new SearchOutcome(FindLiteral(text, query, caseSensitive), PatternValid: true);
    }

    // Ordinal (not culture-aware) keeps the matched length exactly query.Length and avoids culture
    // surprises — also correct under InvariantGlobalization.
    private static List<MatchRange> FindLiteral(string text, string query, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = new List<MatchRange>();
        for (var pos = 0; pos <= text.Length - query.Length;)
        {
            var i = text.IndexOf(query, pos, comparison);
            if (i < 0)
                break;
            matches.Add(new MatchRange(i, query.Length));
            pos = i + query.Length; // non-overlapping
        }

        return matches;
    }

    // ReDoS guard: the find bar recompiles + re-scans the whole document on every keystroke, so a
    // user-typed pathological pattern (e.g. "(a+)+$") against a long line could hang the UI thread
    // with catastrophic backtracking. Cap each Match call; mirrors CodeDecorations' 200 ms budget.
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    private static SearchOutcome FindRegex(string text, string pattern, bool caseSensitive)
    {
        Regex re;
        try
        {
            re = new Regex(pattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return new SearchOutcome(Array.Empty<MatchRange>(), PatternValid: false); // invalid pattern
        }

        var matches = new List<MatchRange>();
        try
        {
            for (var pos = 0; pos <= text.Length;)
            {
                var m = re.Match(text, pos);
                if (!m.Success)
                    break;
                if (m.Length > 0)
                    matches.Add(new MatchRange(m.Index, m.Length));
                // Advance at least one char so a zero-width match can't spin forever.
                pos = m.Index + Math.Max(m.Length, 1);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // The pattern is syntactically valid — it just backtracked past the budget. Return the
            // matches collected so far (often none); the find bar shows partial/no matches, never hangs.
        }

        return new SearchOutcome(matches, PatternValid: true);
    }

    /// <summary>Index into <paramref name="matches"/> of the first match starting after
    /// <paramref name="caretOffset"/>, wrapping to the first; -1 if there are none.</summary>
    public static int NextMatchIndex(IReadOnlyList<MatchRange> matches, int caretOffset)
    {
        if (matches.Count == 0)
            return -1;

        for (var i = 0; i < matches.Count; i++)
            if (matches[i].Offset > caretOffset)
                return i;

        return 0; // wrap to the first
    }

    /// <summary>Index into <paramref name="matches"/> of the last match ending before
    /// <paramref name="caretOffset"/>, wrapping to the last; -1 if there are none.</summary>
    public static int PreviousMatchIndex(IReadOnlyList<MatchRange> matches, int caretOffset)
    {
        if (matches.Count == 0)
            return -1;

        for (var i = matches.Count - 1; i >= 0; i--)
            if (matches[i].Offset + matches[i].Length < caretOffset)
                return i;

        return matches.Count - 1; // wrap to the last
    }
}
