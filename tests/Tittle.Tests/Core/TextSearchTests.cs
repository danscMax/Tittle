using System.Collections.Generic;
using System.Diagnostics;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class TextSearchTests
{
    [Fact]
    public void FindAll_Literal_ReturnsAllMatches_InOrder()
    {
        var r = TextSearch.FindAll("foo bar foo baz foo", "foo");

        Assert.True(r.PatternValid);
        Assert.Equal(new[] { new MatchRange(0, 3), new MatchRange(8, 3), new MatchRange(16, 3) }, r.Matches);
    }

    [Fact]
    public void FindAll_CaseInsensitive_ByDefault_CaseSensitive_WhenAsked()
    {
        Assert.Equal(3, TextSearch.FindAll("Foo foo FOO", "foo").Matches.Count);          // default: insensitive
        Assert.Single(TextSearch.FindAll("Foo foo FOO", "foo", caseSensitive: true).Matches);
    }

    [Fact]
    public void FindAll_Literal_NonOverlapping()
    {
        // "aaaa" with "aa" yields two non-overlapping hits (0 and 2), not three.
        var r = TextSearch.FindAll("aaaa", "aa");

        Assert.Equal(new[] { new MatchRange(0, 2), new MatchRange(2, 2) }, r.Matches);
    }

    [Fact]
    public void FindAll_EmptyTextOrQuery_NoMatches_ButValid()
    {
        Assert.Empty(TextSearch.FindAll("", "x").Matches);
        Assert.Empty(TextSearch.FindAll("x", "").Matches);
        Assert.True(TextSearch.FindAll("x", "").PatternValid);
    }

    [Fact]
    public void FindAll_Regex_Valid_FindsMatches()
    {
        var r = TextSearch.FindAll("a1 b2 c3", "[a-z][0-9]", regex: true);

        Assert.True(r.PatternValid);
        Assert.Equal(new[] { new MatchRange(0, 2), new MatchRange(3, 2), new MatchRange(6, 2) }, r.Matches);
    }

    [Fact]
    public void FindAll_Regex_Invalid_PatternInvalid_NoMatches()
    {
        var r = TextSearch.FindAll("test", "[invalid", regex: true);

        Assert.False(r.PatternValid);
        Assert.Empty(r.Matches);
    }

    [Fact]
    public void FindAll_Regex_ZeroWidthCapable_Terminates_AndSkipsEmpty()
    {
        // "a*" can match empty between chars; the search must not hang and must not record empty hits.
        var r = TextSearch.FindAll("aaa b aaa", "a*", regex: true);

        Assert.True(r.PatternValid);
        Assert.Equal(new[] { new MatchRange(0, 3), new MatchRange(6, 3) }, r.Matches);
    }

    [Fact]
    public void FindAll_Regex_CatastrophicBacktracking_BailsOut_StaysValid()
    {
        // A classic ReDoS pattern against a long run of 'a' would hang for many seconds
        // without a match timeout. The find bar recompiles on every keystroke, so this
        // must bail out fast, never throw, and report the pattern as syntactically valid.
        var text = new string('a', 50_000);

        var sw = Stopwatch.StartNew();
        var r = TextSearch.FindAll(text, "(a+)+$", caseSensitive: false, regex: true);
        sw.Stop();

        Assert.True(r.PatternValid);                       // valid syntax — it just timed out
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void FindAll_Regex_CaseInsensitive_ByDefault()
    {
        Assert.Equal(2, TextSearch.FindAll("Cat cat", "cat", regex: true).Matches.Count);
        Assert.Single(TextSearch.FindAll("Cat cat", "cat", caseSensitive: true, regex: true).Matches);
    }

    [Fact]
    public void NextMatchIndex_FindsAfterCaret_AndWraps()
    {
        var matches = new List<MatchRange> { new(2, 1), new(5, 1), new(8, 1) };

        Assert.Equal(2, TextSearch.NextMatchIndex(matches, 5)); // first offset > 5 is 8 → index 2
        Assert.Equal(0, TextSearch.NextMatchIndex(matches, 8)); // none after → wrap to first
    }

    [Fact]
    public void PreviousMatchIndex_FindsBeforeCaret_AndWraps()
    {
        var matches = new List<MatchRange> { new(2, 1), new(5, 1), new(8, 1) };

        Assert.Equal(0, TextSearch.PreviousMatchIndex(matches, 5)); // last ending before 5 is the [2,1) hit
        Assert.Equal(2, TextSearch.PreviousMatchIndex(matches, 2)); // none before → wrap to last
    }

    [Fact]
    public void NextAndPreviousMatchIndex_Empty_ReturnsMinusOne()
    {
        var none = new List<MatchRange>();

        Assert.Equal(-1, TextSearch.NextMatchIndex(none, 0));
        Assert.Equal(-1, TextSearch.PreviousMatchIndex(none, 0));
    }

    [Fact]
    public void ReplaceAll_Literal_CountsAndReplaces()
        => Assert.Equal(new ReplaceOutcome("X bar X", 2, true), TextSearch.ReplaceAll("foo bar foo", "foo", "X"));

    [Fact]
    public void ReplaceAll_Literal_CaseInsensitiveByDefault_SensitiveWhenAsked()
    {
        Assert.Equal(new ReplaceOutcome("X X", 2, true), TextSearch.ReplaceAll("Foo foo", "foo", "X"));
        Assert.Equal(new ReplaceOutcome("Foo X", 1, true),
            TextSearch.ReplaceAll("Foo foo", "foo", "X", caseSensitive: true));
    }

    [Fact]
    public void ReplaceAll_Literal_NonOverlapping()
        => Assert.Equal(new ReplaceOutcome("bb", 2, true), TextSearch.ReplaceAll("aaaa", "aa", "b"));

    [Fact]
    public void ReplaceAll_Regex_GroupSubstitution()
        => Assert.Equal(new ReplaceOutcome("1a 2b", 2, true),
            TextSearch.ReplaceAll("a1 b2", @"([a-z])([0-9])", "$2$1", regex: true));

    [Fact]
    public void ReplaceAll_Regex_Invalid_LeavesTextUnchanged()
        => Assert.Equal(new ReplaceOutcome("test", 0, false), TextSearch.ReplaceAll("test", "[bad", "x", regex: true));

    [Fact]
    public void ReplaceAll_EmptyQuery_NoChange()
        => Assert.Equal(new ReplaceOutcome("text", 0, true), TextSearch.ReplaceAll("text", "", "x"));

    [Fact]
    public void ReplaceAll_Regex_CatastrophicBacktracking_BailsOut_Unchanged()
    {
        // The non-matching '!' tail makes "(a+)+$" backtrack exponentially (a pure run of 'a' would
        // match on the first greedy pass). The match timeout must bail out and leave the text unchanged.
        var text = new string('a', 50_000) + "!";

        var sw = Stopwatch.StartNew();
        var r = TextSearch.ReplaceAll(text, "(a+)+$", "X", regex: true);
        sw.Stop();

        Assert.True(r.PatternValid);          // valid syntax — it just timed out
        Assert.Equal(0, r.Count);
        Assert.Equal(text, r.NewText);        // bailed out → text unchanged
        Assert.True(sw.ElapsedMilliseconds < 2000, $"took {sw.ElapsedMilliseconds} ms");
    }
}
