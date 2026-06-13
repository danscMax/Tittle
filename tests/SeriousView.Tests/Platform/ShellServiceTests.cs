using System.IO;
using System.Linq;
using SeriousView.Platform;
using Xunit;
using static SeriousView.Platform.ShellService;

namespace SeriousView.Tests.Platform;

// Covers the pure ProcessStartInfo builder behind RevealInExplorer (the Process.Start launch and
// the OS probe are factored out, so every platform branch is testable on any host).
public class ShellServiceTests
{
    [Fact]
    public void Windows_NormalPath_SelectsViaRawArgumentString()
    {
        var psi = ShellService.BuildRevealStartInfo(@"C:\a\b.md", RevealPlatform.Windows);

        Assert.NotNull(psi);
        Assert.Equal("explorer.exe", psi!.FileName);
        Assert.Equal("/select,\"C:\\a\\b.md\"", psi.Arguments);
        Assert.Empty(psi.ArgumentList); // the proven string form, NOT ArgumentList
    }

    [Theory]
    [InlineData("C:\\a\\b\".md")]        // a stray double-quote
    [InlineData("C:\\a\\b.md")]    // a control char (BEL)
    public void Windows_HostilePath_FallsBackToFolderViaArgumentList(string path)
    {
        var psi = ShellService.BuildRevealStartInfo(path, RevealPlatform.Windows);

        Assert.NotNull(psi);
        Assert.Equal("explorer.exe", psi!.FileName);
        Assert.Equal(@"C:\a", Assert.Single(psi.ArgumentList));
        Assert.Equal(string.Empty, psi.Arguments); // no raw /select, string for a hostile path
    }

    [Fact]
    public void MacOS_RevealsWithDashR()
    {
        var psi = ShellService.BuildRevealStartInfo("/Users/x/b.md", RevealPlatform.MacOS);

        Assert.NotNull(psi);
        Assert.Equal("open", psi!.FileName);
        Assert.Equal(new[] { "-R", "/Users/x/b.md" }, psi.ArgumentList.ToArray());
    }

    [Fact]
    public void Linux_OpensContainingFolder()
    {
        const string path = "/home/x/docs/b.md";
        var psi = ShellService.BuildRevealStartInfo(path, RevealPlatform.Linux);

        Assert.NotNull(psi);
        Assert.Equal("xdg-open", psi!.FileName);
        // Expected folder computed the same way the builder does — Path.GetDirectoryName is
        // host-OS-dependent (on Windows it normalises '/'→'\'), so don't hardcode the separator.
        Assert.Equal(Path.GetDirectoryName(path), Assert.Single(psi.ArgumentList));
    }

    [Fact]
    public void NoContainingFolder_IsANoOp()
    {
        // A bare name with no directory: Windows takes the hostile-path (quote) → folder branch and
        // Linux the open-folder branch; both find no folder → null (the launch is skipped).
        Assert.Null(ShellService.BuildRevealStartInfo("b\".md", RevealPlatform.Windows));
        Assert.Null(ShellService.BuildRevealStartInfo("b.md", RevealPlatform.Linux));
    }
}
