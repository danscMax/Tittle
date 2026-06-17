using System.IO;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class RecentFileLabelTests
{
    [Fact]
    public void Describe_SplitsNameAndFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "notes");
        var path = Path.Combine(dir, "readme.md");

        var (name, folder) = RecentFileLabel.Describe(path);

        Assert.Equal("readme.md", name);
        Assert.Equal(dir, folder);
    }

    [Fact]
    public void Describe_BareFileName_HasEmptyFolder()
    {
        var (name, folder) = RecentFileLabel.Describe("readme.md");

        Assert.Equal("readme.md", name);
        Assert.Equal(string.Empty, folder);
    }

    [Fact]
    public void Describe_NullOrBlank_DegradesToEmpty()
    {
        Assert.Equal((string.Empty, string.Empty), RecentFileLabel.Describe(null));
        Assert.Equal((string.Empty, string.Empty), RecentFileLabel.Describe("   "));
    }
}
