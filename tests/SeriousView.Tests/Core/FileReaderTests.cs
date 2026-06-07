using System.IO;
using System.Text;
using System.Threading.Tasks;
using SeriousView.Core.Documents;
using SeriousView.Core.Services;
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
}
