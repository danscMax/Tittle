using System.Text;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class SaveEncodingTests
{
    [Fact]
    public void GetBytes_Utf8_NoBom()
        => Assert.Equal(new byte[] { 0x41 }, SaveEncoding.GetBytes("A", SaveEncoding.Utf8));

    [Fact]
    public void GetBytes_Utf8Bom_PrependsPreamble()
        => Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, SaveEncoding.GetBytes("A", SaveEncoding.Utf8Bom));

    [Fact]
    public void GetBytes_Utf16Le_WithBom()
        => Assert.Equal(new byte[] { 0xFF, 0xFE, 0x41, 0x00 }, SaveEncoding.GetBytes("A", SaveEncoding.Utf16Le));

    [Fact]
    public void GetBytes_Utf16Be_WithBom()
        => Assert.Equal(new byte[] { 0xFE, 0xFF, 0x00, 0x41 }, SaveEncoding.GetBytes("A", SaveEncoding.Utf16Be));

    [Fact]
    public void GetBytes_Windows1251_Cyrillic_RoundTrips()
    {
        var bytes = SaveEncoding.GetBytes("Яя", SaveEncoding.Windows1251); // registers the code-pages provider

        Assert.Equal(new byte[] { 0xDF, 0xFF }, bytes); // Я=0xDF, я=0xFF in CP1251
        Assert.Equal("Яя", Encoding.GetEncoding(1251).GetString(bytes));
    }

    [Fact]
    public void GetBytes_UnknownName_FallsBackToUtf8NoBom()
        => Assert.Equal(new byte[] { 0x41 }, SaveEncoding.GetBytes("A", "totally-bogus"));

    [Fact]
    public void Decode_Windows1251Bytes()
        => Assert.Equal("Яя", SaveEncoding.Decode(new byte[] { 0xDF, 0xFF }, SaveEncoding.Windows1251));

    [Fact]
    public void Decode_StripsAMatchingBom()
    {
        Assert.Equal("A", SaveEncoding.Decode(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, SaveEncoding.Utf8Bom));
        Assert.Equal("A", SaveEncoding.Decode(new byte[] { 0xFF, 0xFE, 0x41, 0x00 }, SaveEncoding.Utf16Le));
    }

    [Fact]
    public void Decode_Utf8NoBom()
        => Assert.Equal("A", SaveEncoding.Decode(new byte[] { 0x41 }, SaveEncoding.Utf8));
}
