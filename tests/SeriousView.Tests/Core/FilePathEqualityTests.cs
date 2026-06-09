using System.IO;
using SeriousView.Core.Services;
using Xunit;

namespace SeriousView.Tests.Core;

public class FilePathEqualityTests
{
    [Fact]
    public void SameFile_True_ForIdenticalPath()
        => Assert.True(FilePathEquality.SameFile("/docs/readme.md", "/docs/readme.md"));

    [Fact]
    public void SameFile_True_IgnoringCase()
        => Assert.True(FilePathEquality.SameFile("readme.md", "README.MD"));

    [Fact]
    public void SameFile_True_ForRelativeAndAbsolute_SameTarget()
    {
        var absolute = Path.Combine(Directory.GetCurrentDirectory(), "notes.txt");
        Assert.True(FilePathEquality.SameFile("notes.txt", absolute));
    }

    [Fact]
    public void SameFile_True_ResolvingDotSegments()
        => Assert.True(FilePathEquality.SameFile("/a/b/../b/c.md", "/a/b/c.md"));

    [Fact]
    public void SameFile_False_ForDifferentFiles()
        => Assert.False(FilePathEquality.SameFile("/docs/a.md", "/docs/b.md"));

    [Fact]
    public void SameFile_False_WhenEitherNullOrEmpty()
    {
        Assert.False(FilePathEquality.SameFile(null, "/a.md"));
        Assert.False(FilePathEquality.SameFile("/a.md", null));
        Assert.False(FilePathEquality.SameFile(null, null));
        Assert.False(FilePathEquality.SameFile("", "/a.md"));
    }
}
