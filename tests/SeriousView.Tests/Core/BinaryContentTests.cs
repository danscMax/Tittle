using System;
using System.Linq;
using System.Text;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class BinaryContentTests
{
    [Fact]
    public void PlainText_IsNotBinary()
        => Assert.False(BinaryContent.IsProbablyBinary(Encoding.UTF8.GetBytes("hello\nworld")));

    [Fact]
    public void NulByte_IsBinary()
        => Assert.True(BinaryContent.IsProbablyBinary(new byte[] { (byte)'a', 0x00, (byte)'b' }));

    [Fact]
    public void Utf16WithBom_IsNotBinary_DespiteNulBytes()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("abc")).ToArray();
        Assert.False(BinaryContent.IsProbablyBinary(bytes));
    }

    [Fact]
    public void Empty_IsNotBinary()
        => Assert.False(BinaryContent.IsProbablyBinary(Array.Empty<byte>()));
}
