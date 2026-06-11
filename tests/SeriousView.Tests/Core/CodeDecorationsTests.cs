using System;
using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class CodeDecorationsTests
{
    private static readonly DateOnly Today = new(2026, 6, 11);

    private static CodeDecoration Single(string line, CodeDecorationKind kind)
    {
        var hits = CodeDecorations.ScanLine(line, Today).Where(d => d.Kind == kind).ToList();
        Assert.Single(hits);
        return hits[0];
    }

    private static string TextOf(string line, CodeDecoration d) => line.Substring(d.Start, d.Length);

    [Theory]
    [InlineData("2026-06-11T14:23:01Z fail", "2026-06-11T14:23:01Z")]
    [InlineData("at 2026-06-11 14:23:01.123 ok", "2026-06-11 14:23:01.123")]
    public void Timestamps_AreDecorated(string line, string expected)
        => Assert.Equal(expected, TextOf(line, Single(line, CodeDecorationKind.Timestamp)));

    [Fact]
    public void Uuid_IsDecorated()
    {
        const string line = "id=123e4567-e89b-12d3-a456-426614174000;";
        Assert.Equal("123e4567-e89b-12d3-a456-426614174000",
            TextOf(line, Single(line, CodeDecorationKind.Uuid)));
    }

    [Fact]
    public void MacAddress_IsDecorated()
    {
        const string line = "nic 00:1B:44:11:3A:B7 up";
        Assert.Equal("00:1B:44:11:3A:B7", TextOf(line, Single(line, CodeDecorationKind.Mac)));
    }

    [Fact]
    public void IpAddress_IsDecorated()
    {
        const string line = "from 192.168.1.10 port 443";
        Assert.Equal("192.168.1.10", TextOf(line, Single(line, CodeDecorationKind.Ip)));
    }

    [Fact]
    public void Email_IsDecorated()
    {
        const string line = "contact user.name+tag@example.co.uk now";
        Assert.Equal("user.name+tag@example.co.uk", TextOf(line, Single(line, CodeDecorationKind.Email)));
    }

    [Theory]
    [InlineData("d41d8cd98f00b204e9800998ecf8427e")]                                  // md5 (32)
    [InlineData("da39a3ee5e6b4b0d3255bfef95601890afd80709")]                          // sha1 (40)
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]  // sha256 (64)
    public void Hashes_AreDecorated(string hash)
    {
        var line = $"sum {hash} ok";
        Assert.Equal(hash, TextOf(line, Single(line, CodeDecorationKind.Hash)));
    }

    [Fact]
    public void ArbitraryHex_IsNotAHash()
    {
        const string line = "color #aabbcc and deadbeef";
        Assert.DoesNotContain(CodeDecorations.ScanLine(line, Today),
            d => d.Kind == CodeDecorationKind.Hash);
    }

    [Fact]
    public void FileLineColPath_IsDecorated()
    {
        const string line = "  at Program.cs:42:13 in Main";
        Assert.Equal("Program.cs:42:13", TextOf(line, Single(line, CodeDecorationKind.FilePath)));
    }

    [Theory]
    [InlineData("TODO")]
    [InlineData("FIXME")]
    [InlineData("HACK")]
    public void TodoMarkers_AreDecorated(string marker)
    {
        var line = $"// {marker}: clean this up";
        Assert.Equal(marker, TextOf(line, Single(line, CodeDecorationKind.Todo)));
    }

    [Fact]
    public void LowercaseTodo_IsIgnored()
        => Assert.DoesNotContain(CodeDecorations.ScanLine("// todo: later", Today),
            d => d.Kind == CodeDecorationKind.Todo);

    [Theory]
    [InlineData("ERROR")]
    [InlineData("WARN")]
    [InlineData("INFO")]
    [InlineData("DEBUG")]
    public void LogLevels_AreDecorated(string level)
    {
        var line = $"[{level}] something happened";
        Assert.Equal(level, TextOf(line, Single(line, CodeDecorationKind.LogLevel)));
    }

    [Fact]
    public void HtmlEntity_CarriesDecodedTooltip()
    {
        const string line = "use &mdash; here";
        var d = Single(line, CodeDecorationKind.HtmlEntity);
        Assert.Equal("&mdash;", TextOf(line, d));
        Assert.Equal("—", d.Tooltip);
    }

    [Fact]
    public void NumericEntity_CarriesDecodedTooltip()
    {
        const string line = "char &#x1F600; smile";
        var d = Single(line, CodeDecorationKind.HtmlEntity);
        Assert.Equal("😀", d.Tooltip);
    }

    [Fact]
    public void UnknownEntity_IsNotDecorated()
        => Assert.DoesNotContain(CodeDecorations.ScanLine("see &notathing; here", Today),
            d => d.Kind == CodeDecorationKind.HtmlEntity);

    [Theory]
    [InlineData("size 5 MB total", "5 MB", "5 242 880 байт")]
    [InlineData("only 42%", "42%", "0,42")]
    [InlineData("about 3 млн runs", "3 млн", "3 000 000")]
    public void Units_CarryExpansionTooltips(string line, string token, string tooltip)
    {
        var d = Single(line, CodeDecorationKind.Unit);
        Assert.Equal(token, TextOf(line, d));
        Assert.Equal(tooltip, d.Tooltip);
    }

    [Fact]
    public void SymbolTerminatedUnit_Matches()
    {
        // The original's trailing \b silently failed on %, ₽, € — the lookahead must not.
        const string line = "цена 1500 ₽ всего";
        Assert.Equal("1500 ₽", TextOf(line, Single(line, CodeDecorationKind.Unit)));
    }

    [Theory]
    [InlineData("встреча 13.06.2026 утром", "13.06.2026", "через 2 дня")]
    [InlineData("сдано 2026-06-10 вечером", "2026-06-10", "1 день назад")]
    [InlineData("дедлайн 11.06.2026!", "11.06.2026", "сегодня")]
    public void Dates_CarryRelativeTooltips(string line, string token, string tooltip)
    {
        var d = Single(line, CodeDecorationKind.Date);
        Assert.Equal(token, TextOf(line, d));
        Assert.Equal(tooltip, d.Tooltip);
    }

    [Fact]
    public void RussianMonthName_IsADate()
    {
        const string line = "отчёт за февраля 2026 готов";
        var d = Single(line, CodeDecorationKind.Date);
        Assert.Equal("февраля 2026", TextOf(line, d));
    }

    [Fact]
    public void IsoTimestamp_WinsOverDate()
    {
        // 'ts' is evaluated before 'date' in the alternation, as in the original.
        const string line = "2026-06-11T10:00:00Z";
        var all = CodeDecorations.ScanLine(line, Today);
        Assert.Contains(all, d => d.Kind == CodeDecorationKind.Timestamp);
        Assert.DoesNotContain(all, d => d.Kind == CodeDecorationKind.Date);
    }

    [Fact]
    public void OverlongLine_IsLeftUndecorated()
    {
        var line = new string('a', 2001) + " TODO";
        Assert.Empty(CodeDecorations.ScanLine(line, Today));
    }

    [Fact]
    public void MultipleKinds_AllFoundWithCorrectOffsets()
    {
        const string line = "[ERROR] 10.0.0.1 user@host.io TODO";
        var all = CodeDecorations.ScanLine(line, Today);

        Assert.Equal(4, all.Count);
        foreach (var d in all)
            Assert.True(d.Start >= 0 && d.Start + d.Length <= line.Length);
        // Decorations come back in document order.
        Assert.True(all.Zip(all.Skip(1), (a, b) => a.Start < b.Start).All(x => x));
    }

    [Fact]
    public void PlainProse_HasNoDecorations()
        => Assert.Empty(CodeDecorations.ScanLine("обычный текст без токенов", Today));
}
