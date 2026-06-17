using System.Linq;
using System.Text;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class TextEncodingDetectorTests
{
    [Fact]
    public void Decode_Utf8NoBom_Cyrillic()
    {
        var (text, name) = TextEncodingDetector.Decode(Encoding.UTF8.GetBytes("Привет, мир"));
        Assert.Equal("Привет, мир", text);
        Assert.Equal("UTF-8", name);
    }

    [Fact]
    public void Decode_Utf8Bom_IsStripped()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("Hi")).ToArray();
        var (text, name) = TextEncodingDetector.Decode(bytes);
        Assert.Equal("Hi", text);
        Assert.Equal("UTF-8", name);
        Assert.DoesNotContain('﻿', text);
    }

    [Fact]
    public void Decode_Utf16Le_WithBom()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Тест")).ToArray();
        var (text, name) = TextEncodingDetector.Decode(bytes);
        Assert.Equal("Тест", text);
        Assert.Equal("UTF-16 LE", name);
    }

    [Fact]
    public void Decode_Utf16Be_WithBom()
    {
        var bytes = Encoding.BigEndianUnicode.GetPreamble()
            .Concat(Encoding.BigEndianUnicode.GetBytes("Тест")).ToArray();
        var (text, name) = TextEncodingDetector.Decode(bytes);
        Assert.Equal("Тест", text);
        Assert.Equal("UTF-16 BE", name);
    }

    [Fact]
    public void Decode_Windows1251_WhenNotValidUtf8()
    {
        // "Привет" in Windows-1251 — invalid as UTF-8, so the detector falls back to 1251.
        var bytes = new byte[] { 0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2 };
        var (text, name) = TextEncodingDetector.Decode(bytes);
        Assert.Equal("Привет", text);
        Assert.Equal("Windows-1251", name);
    }

    [Fact]
    public void Decode_Ascii_IsUtf8()
    {
        var (text, name) = TextEncodingDetector.Decode(Encoding.ASCII.GetBytes("plain ascii"));
        Assert.Equal("plain ascii", text);
        Assert.Equal("UTF-8", name);
    }
}
