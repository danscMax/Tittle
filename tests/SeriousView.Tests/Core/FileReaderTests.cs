using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SeriousView.Core.Documents;
using SeriousView.Core.Services;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class FileReaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsUtf8Text_NormalizingLineEndings()
    {
        var reader = new FileReader();
        var temp = Path.GetTempFileName();
        await File.WriteAllTextAsync(temp, "hello\r\nworld", new UTF8Encoding(false));
        try
        {
            var result = await reader.LoadAsync(temp);

            Assert.Equal(FileLoadKind.Text, result.Kind);
            Assert.Equal("hello\nworld", result.Text);   // CRLF normalized to LF
            Assert.Equal("UTF-8", result.EncodingName);
            Assert.Equal("CRLF", result.LineEnding);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public async Task LoadAsync_DetectsBinary()
    {
        var reader = new FileReader();
        var temp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(temp, new byte[] { 0x89, 0x50, 0x00, 0x4E, 0x47 });
        try
        {
            var result = await reader.LoadAsync(temp);
            Assert.Equal(FileLoadKind.Binary, result.Kind);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public async Task LoadAsync_MissingFile_Throws()
    {
        var reader = new FileReader();
        await Assert.ThrowsAnyAsync<IOException>(
            () => reader.LoadAsync(Path.Combine(Path.GetTempPath(), "sv_no_such_file_xyz.txt")));
    }

    [Fact]
    public async Task LoadAsync_LargeText_RoundTripsAcrossTheHeadBoundary()
    {
        // Larger than BinaryContent.ScanWindow so the read spans the head plus the separately-read
        // tail; guards that the two-part read reconstructs the bytes without a seam at the boundary.
        var reader = new FileReader();
        var content = new string('x', BinaryContent.ScanWindow * 2 + 123);
        var temp = Path.GetTempFileName();
        await File.WriteAllTextAsync(temp, content, new UTF8Encoding(false));
        try
        {
            var result = await reader.LoadAsync(temp);

            Assert.Equal(FileLoadKind.Text, result.Kind);
            Assert.Equal(content, result.Text);
        }
        finally { File.Delete(temp); }
    }

    [Fact]
    public async Task LoadAsync_LargeBinary_DetectedFromHead()
    {
        // A NUL inside the first ScanWindow bytes of a file far larger than it: the head-only scan
        // still flags it binary without reading the whole file.
        var reader = new FileReader();
        var bytes = new byte[BinaryContent.ScanWindow * 3];
        Array.Fill(bytes, (byte)0x41); // 'A'
        bytes[100] = 0x00;             // NUL in the head → binary
        var temp = Path.GetTempFileName();
        await File.WriteAllBytesAsync(temp, bytes);
        try
        {
            var result = await reader.LoadAsync(temp);
            Assert.Equal(FileLoadKind.Binary, result.Kind);
        }
        finally { File.Delete(temp); }
    }
}
