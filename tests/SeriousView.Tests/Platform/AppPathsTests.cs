using System;
using System.IO;
using SeriousView.Platform;
using Xunit;

namespace SeriousView.Tests.Platform;

public class AppPathsTests
{
    [Fact]
    public void DataDir_IsTheSeriousViewFolderUnderAppData()
    {
        var dir = AppPaths.DataDir;

        Assert.Equal("SeriousView", Path.GetFileName(dir));
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), dir);
    }
}
