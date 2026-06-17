using System.Collections.Generic;
using Tittle.Core.Diagnostics;
using Xunit;

namespace Tittle.Tests.Core;

public class SingleInstanceMessageTests
{
    private static IReadOnlyList<string> RoundTrip(params string[] args)
        => SingleInstanceMessage.Decode(SingleInstanceMessage.Encode(args));

    [Fact]
    public void RoundTrips_SingleAsciiPath()
        => Assert.Equal(new[] { @"C:\src\readme.md" }, RoundTrip(@"C:\src\readme.md"));

    [Fact]
    public void RoundTrips_PathWithSpaces()
        => Assert.Equal(new[] { @"C:\Users\Max Scorpy\My Notes\readme.md" },
                        RoundTrip(@"C:\Users\Max Scorpy\My Notes\readme.md"));

    [Fact]
    public void RoundTrips_UnicodePath()
        => Assert.Equal(new[] { @"C:\Документы\заметка.md" }, RoundTrip(@"C:\Документы\заметка.md"));

    [Fact]
    public void RoundTrips_MultiplePaths_InOrder()
        => Assert.Equal(new[] { "/a.md", "/b.md", "/c.md" }, RoundTrip("/a.md", "/b.md", "/c.md"));

    [Fact]
    public void Encode_DropsEmptyAndWhitespaceArgs()
        => Assert.Equal(new[] { "/real.md" }, RoundTrip("", "   ", "/real.md"));

    [Theory]
    [InlineData("")]
    [InlineData("not a real payload")]      // no header → ignored
    [InlineData("SV2/other.md")]            // wrong header → ignored
    public void Decode_GarbageOrWrongHeader_IsEmpty(string text)
        => Assert.Empty(SingleInstanceMessage.Decode(System.Text.Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void Decode_EmptyPayload_IsEmpty()
        => Assert.Empty(SingleInstanceMessage.Decode(System.ReadOnlySpan<byte>.Empty));

    [Fact]
    public void Encode_StripsSeparatorAndNewlines_SoOneArgCannotSplit()
    {
        // An arg carrying the separator () or newlines must stay a single decoded entry.
        var hostile = "ab\nc";
        var decoded = RoundTrip(hostile);
        Assert.Single(decoded);
        Assert.DoesNotContain('', decoded[0]);
        Assert.DoesNotContain('\n', decoded[0]);
    }

    [Fact]
    public void Names_AreDeterministic_Distinct_AndPathSafe()
    {
        var mutex = SingleInstanceMessage.MutexName("Max");
        var pipe = SingleInstanceMessage.PipeName("Max");

        Assert.Equal(mutex, SingleInstanceMessage.MutexName("Max"));   // deterministic
        Assert.NotEqual(mutex, pipe);                                  // mutex != pipe
        Assert.NotEqual(mutex, SingleInstanceMessage.MutexName("Bob")); // per-user
        foreach (var name in new[] { mutex, pipe })
        {
            Assert.DoesNotContain(' ', name);
            Assert.DoesNotContain('\\', name);
        }
    }

    [Fact]
    public void Names_EmptyUser_AreNonEmpty()
        => Assert.False(string.IsNullOrEmpty(SingleInstanceMessage.MutexName("")));
}
