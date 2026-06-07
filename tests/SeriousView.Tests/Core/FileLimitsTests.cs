using SeriousView.Core.Documents;
using Xunit;

namespace SeriousView.Tests.Core;

public class FileLimitsTests
{
    [Fact]
    public void SmallFile_LoadsWithHighlight()
    {
        Assert.False(FileLimits.IsTooLarge(1024));
        Assert.False(FileLimits.SuppressHighlight(1024));
    }

    [Fact]
    public void MidFile_LoadsButSuppressesHighlight()
    {
        const long size = 10L * 1024 * 1024; // 10 MB
        Assert.False(FileLimits.IsTooLarge(size));
        Assert.True(FileLimits.SuppressHighlight(size));
    }

    [Fact]
    public void HugeFile_IsTooLarge()
        => Assert.True(FileLimits.IsTooLarge(60L * 1024 * 1024));

    [Fact]
    public void ForText_DerivesHighlightSuppressed_FromSize()
    {
        var small = FileLoadResult.ForText("hi", "UTF-8", "LF", 100);
        Assert.Equal(FileLoadKind.Text, small.Kind);
        Assert.False(small.HighlightSuppressed);

        var big = FileLoadResult.ForText("hi", "UTF-8", "LF", 10L * 1024 * 1024);
        Assert.True(big.HighlightSuppressed);
    }
}
