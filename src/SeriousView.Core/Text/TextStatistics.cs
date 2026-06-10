using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>Document statistics for the stats panel (ported): words, characters, sentences,
/// reading time (~200 wpm) and a Russian-adapted Flesch readability score (syllables counted
/// as vowels, Cyrillic + Latin).</summary>
public sealed record TextStats(
    int Words, int Chars, int CharsNoSpaces, int Sentences, int ReadingMinutes, double FleschRu);

public static partial class TextStatistics
{
    private const int WordsPerMinute = 200;
    private const string Vowels = "аеёиоуыэюяaeiouy";

    public static TextStats Compute(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TextStats(0, 0, 0, 0, 0, 0);

        var words = CountWords(text);
        var chars = text.Length;
        var charsNoSpaces = text.Count(c => !char.IsWhiteSpace(c));
        var sentences = Math.Max(1, SentenceEnd().Matches(text).Count);
        var minutes = Math.Max(1, (int)Math.Round(words / (double)WordsPerMinute));

        var syllables = text.ToLowerInvariant().Count(c => Vowels.Contains(c));
        var flesch = words == 0
            ? 0
            : 206.835 - 1.3 * ((double)words / sentences) - 60.1 * ((double)syllables / words);

        return new TextStats(words, chars, charsNoSpaces, sentences, minutes, Math.Round(flesch, 1));
    }

    /// <summary>The shared word-count idiom (also used for the selection counter).</summary>
    public static int CountWords(string? text)
        => string.IsNullOrWhiteSpace(text) ? 0 : WordToken().Matches(text).Count;

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordToken();

    [GeneratedRegex(@"[.!?…]+(?=\s|$)")]
    private static partial Regex SentenceEnd();
}
