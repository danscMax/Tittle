using System;
using System.IO;
using Tittle.Platform;
using Xunit;

namespace Tittle.Tests.Platform;

public class AppPathsTests
{
    [Fact]
    public void DataDir_IsTheTittleFolderUnderAppData()
    {
        var dir = AppPaths.DataDir;

        Assert.Equal("Tittle", Path.GetFileName(dir));
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), dir);
    }
}
