using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class TextStatisticsTests
{
    [Fact]
    public void Compute_CountsWordsCharsAndSentences()
    {
        var stats = TextStatistics.Compute("Привет, мир! Это второй абзац. А это — третий?");

        Assert.Equal(9, stats.Words); // the \S+ idiom counts the standalone dash too (original behavior)
        Assert.Equal(3, stats.Sentences);
        Assert.True(stats.Chars > stats.CharsNoSpaces);
    }

    [Fact]
    public void Compute_ReadingTime_ScalesWithLength()
    {
        var shortText = TextStatistics.Compute("одно слово");
        var longText = TextStatistics.Compute(string.Join(" ",
            System.Linq.Enumerable.Repeat("слово", 1000)));

        Assert.True(longText.ReadingMinutes > shortText.ReadingMinutes);
        Assert.True(longText.ReadingMinutes >= 4); // ~1000 words at ~200 wpm
    }

    [Fact]
    public void Compute_FleschRu_SimpleTextScoresHigherThanComplex()
    {
        var simple = TextStatistics.Compute("Кот спал. Пёс ел. Дом был тих.");
        var complex = TextStatistics.Compute(
            "Многофункциональная интероперабельность инфраструктурных подсистем " +
            "детерминирует экспоненциальную мультипликацию организационных взаимозависимостей.");

        Assert.True(simple.FleschRu > complex.FleschRu);
    }

    [Fact]
    public void Compute_EmptyText_IsAllZeros()
    {
        var stats = TextStatistics.Compute("");

        Assert.Equal(0, stats.Words);
        Assert.Equal(0, stats.Sentences);
    }

    [Fact]
    public void CountWords_SharedHelper()
    {
        Assert.Equal(3, TextStatistics.CountWords("раз два три"));
        Assert.Equal(0, TextStatistics.CountWords("   "));
    }
}
