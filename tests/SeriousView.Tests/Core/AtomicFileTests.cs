using System.IO;
using System.Text;
using System.Threading.Tasks;
using SeriousView.Core.Support;
using Xunit;

namespace SeriousView.Tests.Core;

public class AtomicFileTests
{
    [Fact]
    public async Task WriteAllTextAsync_CreatesANewFile()
    {
        var dir = Directory.CreateTempSubdirectory("sv-atomic");
        try
        {
            var path = Path.Combine(dir.FullName, "new.txt");
            await AtomicFile.WriteAllTextAsync(path, "hello мир");

            Assert.True(File.Exists(path));
            Assert.Equal("hello мир", File.ReadAllText(path));
            Assert.False(File.Exists(path + ".tmp")); // the temp was swapped, not left behind
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteAllTextAsync_OverwritesAnExistingFile_Atomically()
    {
        var dir = Directory.CreateTempSubdirectory("sv-atomic");
        try
        {
            var path = Path.Combine(dir.FullName, "doc.md");
            File.WriteAllText(path, "# old content that is longer");

            await AtomicFile.WriteAllTextAsync(path, "# new");

            Assert.Equal("# new", File.ReadAllText(path)); // fully replaced, not appended/truncated
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteAllTextAsync_WritesUtf8WithoutABom()
    {
        var dir = Directory.CreateTempSubdirectory("sv-atomic");
        try
        {
            var path = Path.Combine(dir.FullName, "enc.txt");
            await AtomicFile.WriteAllTextAsync(path, "Ω");

            var bytes = File.ReadAllBytes(path);
            // No UTF-8 BOM (EF BB BF) prefix — matches the app's prior File.WriteAllTextAsync policy.
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            Assert.Equal("Ω", Encoding.UTF8.GetString(bytes));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteAllBytesAsync_RoundTrips_AndOverwritesAtomically()
    {
        var dir = Directory.CreateTempSubdirectory("sv-atomic");
        try
        {
            var path = Path.Combine(dir.FullName, "bin.dat");
            File.WriteAllText(path, "old content that is longer");
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }; // a UTF-8 BOM + 'A'

            await AtomicFile.WriteAllBytesAsync(path, bytes);

            Assert.Equal(bytes, File.ReadAllBytes(path)); // fully replaced with the exact bytes
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
