using System.IO;
using SeriousView.Core.Services;
using Xunit;

namespace SeriousView.Tests.Core;

public class RecentFilePathPolicyTests
{
    [Fact]
    public void FileUnderRoot_IsDetected()
    {
        var root = Path.Combine(Path.GetTempPath(), "sv-policy");
        Assert.True(RecentFilePathPolicy.IsUnderTempFolder(Path.Combine(root, "sub", "a.cs"), root));
    }

    [Fact]
    public void PathEqualToRoot_IsDetected()
    {
        var root = Path.Combine(Path.GetTempPath(), "sv-policy");
        Assert.True(RecentFilePathPolicy.IsUnderTempFolder(root, root));
    }

    [Fact]
    public void SiblingSharingPrefix_IsNotUnderRoot()
    {
        // "…/Temp2/f" must NOT match root "…/Temp" (the prefix-without-separator hazard).
        var baseDir = Path.GetTempPath();
        var root = Path.Combine(baseDir, "Temp");
        var sibling = Path.Combine(baseDir, "Temp2", "f.cs");
        Assert.False(RecentFilePathPolicy.IsUnderTempFolder(sibling, root));
    }

    [Fact]
    public void FileOutsideRoot_IsNotUnderRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "sv-policy");
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "a.cs");
        Assert.False(RecentFilePathPolicy.IsUnderTempFolder(outside, root));
    }

    [Fact]
    public void DifferentCase_StillDetected()
    {
        // Policy is case-insensitive by design (Windows/macOS temp paths are).
        var root = Path.Combine(Path.GetTempPath(), "SV-Policy");
        var path = Path.Combine(Path.GetTempPath(), "sv-policy", "a.cs");
        Assert.True(RecentFilePathPolicy.IsUnderTempFolder(path, root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmptyPath_IsFalse(string? path)
        => Assert.False(RecentFilePathPolicy.IsUnderTempFolder(path, Path.GetTempPath()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyRoot_IsFalse(string? root)
        => Assert.False(RecentFilePathPolicy.IsUnderTempFolder(Path.Combine(Path.GetTempPath(), "a.cs"), root));
}
