using System.IO;
using System.Threading.Tasks;
using SeriousView.Core.Services;
using Xunit;

namespace SeriousView.Tests.Core;

public class FileReaderTests
{
    [Fact]
    public void Exists_IsTrueForRealFile_FalseForMissing()
    {
        var reader = new FileReader();
        var temp = Path.GetTempFileName();
        try
        {
            Assert.True(reader.Exists(temp));
            Assert.False(reader.Exists(temp + ".missing"));
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public async Task ReadAllText_ReturnsFileContent_SyncAndAsync()
    {
        var reader = new FileReader();
        var temp = Path.GetTempFileName();
        await File.WriteAllTextAsync(temp, "hello\nworld");
        try
        {
            Assert.Equal("hello\nworld", reader.ReadAllText(temp));
            Assert.Equal("hello\nworld", await reader.ReadAllTextAsync(temp));
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
