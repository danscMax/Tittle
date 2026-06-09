using System;
using System.Collections.Generic;

namespace SeriousView.Core.Commands;

/// <summary>A successful fuzzy match: a relevance <see cref="Score"/> (higher is better) and the
/// candidate character <see cref="Indices"/> that matched, for highlighting.</summary>
public readonly record struct FuzzyMatch(int Score, IReadOnlyList<int> Indices);

/// <summary>
/// fzf-lite subsequence matcher for the command palette: case-insensitive, every query character must
/// appear in order in the candidate (e.g. <c>opfil</c> → <c>Open File</c>), scored by match position
/// (start / word boundary) and contiguity, returning the matched indices for highlight. Pure, UI-free.
/// (No well-maintained .NET NuGet does fzf-style subsequence + highlight indices; the popular FuzzyWuzzy
/// ports are ratio-based and weak for command abbreviations — so this small algorithm lives in Core.)
/// </summary>
public static class FuzzyMatcher
{
    // Scoring weights, tuned for short command titles.
    private const int MatchBase = 16;      // each matched char
    private const int StartBonus = 12;     // match at index 0
    private const int BoundaryBonus = 10;  // match right after a separator / at a camelCase boundary
    private const int ContiguousBonus = 8; // match adjacent to the previous matched char
    private const int GapPenalty = 1;      // per skipped char between matches (capped)

    /// <summary>Returns null when <paramref name="query"/> is not a subsequence of
    /// <paramref name="candidate"/>; an empty/whitespace query matches everything with score 0.</summary>
    public static FuzzyMatch? Match(string query, string candidate)
    {
        if (candidate is null)
            return null;
        if (string.IsNullOrWhiteSpace(query))
            return new FuzzyMatch(0, Array.Empty<int>());

        var indices = new List<int>(query.Length);
        var score = 0;
        var cursor = 0;
        var prev = -1;

        foreach (var qc in query)
        {
            if (char.IsWhiteSpace(qc))
                continue; // ignore spaces in the query so "op fi" still matches "Open File"

            var found = IndexOfFrom(candidate, qc, cursor);
            if (found < 0)
                return null; // a query char is missing (in order) → no match

            score += MatchBase;
            if (found == 0)
                score += StartBonus;
            else if (IsBoundary(candidate, found))
                score += BoundaryBonus;

            if (prev >= 0)
            {
                if (found == prev + 1)
                    score += ContiguousBonus;
                else
                    score -= Math.Min(found - prev - 1, 8) * GapPenalty;
            }

            indices.Add(found);
            prev = found;
            cursor = found + 1;
        }

        return indices.Count == 0
            ? new FuzzyMatch(0, Array.Empty<int>()) // query was all whitespace
            : new FuzzyMatch(score, indices);
    }

    private static int IndexOfFrom(string s, char c, int from)
    {
        var lc = char.ToLowerInvariant(c);
        for (var i = from; i < s.Length; i++)
            if (char.ToLowerInvariant(s[i]) == lc)
                return i;
        return -1;
    }

    // A match starts a "word" if it follows a separator, or is an uppercase letter after a lowercase one.
    private static bool IsBoundary(string s, int i)
    {
        var p = s[i - 1];
        return p is ' ' or '-' or '_' or '/' or '.' or ':'
            || (char.IsUpper(s[i]) && char.IsLower(p));
    }
}
