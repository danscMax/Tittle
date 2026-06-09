using SeriousView.Core.Commands;
using Xunit;

namespace SeriousView.Tests.Core;

public class FuzzyMatcherTests
{
    [Fact]
    public void Match_Abbreviation_MatchesSubsequence_WithIndices()
    {
        var m = FuzzyMatcher.Match("opfil", "Open File");

        Assert.NotNull(m);
        // O p . . . F i l  → highlights "Op" and "Fil"
        Assert.Equal(new[] { 0, 1, 5, 6, 7 }, m!.Value.Indices);
    }

    [Fact]
    public void Match_NotASubsequence_ReturnsNull()
    {
        Assert.Null(FuzzyMatcher.Match("xyz", "Open File"));
        Assert.Null(FuzzyMatcher.Match("op", "Profile")); // no 'p' after the 'o'
    }

    [Fact]
    public void Match_EmptyOrWhitespaceQuery_MatchesAll_WithScoreZero()
    {
        var m = FuzzyMatcher.Match("   ", "Anything");

        Assert.NotNull(m);
        Assert.Equal(0, m!.Value.Score);
        Assert.Empty(m.Value.Indices);
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        var m = FuzzyMatcher.Match("OPEN", "open file");

        Assert.NotNull(m);
        Assert.Equal(new[] { 0, 1, 2, 3 }, m!.Value.Indices);
    }

    [Fact]
    public void Match_StartAnchored_OutranksMidWord()
    {
        var atStart = FuzzyMatcher.Match("set", "Settings");
        var midWord = FuzzyMatcher.Match("set", "Reset Tab");

        Assert.NotNull(atStart);
        Assert.NotNull(midWord);
        Assert.True(atStart!.Value.Score > midWord!.Value.Score);
    }

    [Fact]
    public void Match_WordBoundary_OutranksScattered()
    {
        // 'of' hits the 'O' start + the 'F' word boundary in "Open File"; in "Footer" it is scattered.
        var boundary = FuzzyMatcher.Match("of", "Open File");
        var scattered = FuzzyMatcher.Match("of", "Footer of");

        Assert.NotNull(boundary);
        Assert.NotNull(scattered);
        Assert.True(boundary!.Value.Score > scattered!.Value.Score);
    }
}
