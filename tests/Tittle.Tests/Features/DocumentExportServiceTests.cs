using System.IO;
using System.Threading.Tasks;
using Tittle.Features.Shell;
using Xunit;

namespace Tittle.Tests.Features;

public class DocumentExportServiceTests
{
    private static DocumentExportService Create(FakeClipboardService? clipboard = null, FakeShellService? shell = null)
        => new(new FakeThemeService(), shell ?? new FakeShellService(), clipboard ?? new FakeClipboardService());

    [Fact]
    public async Task ExportHtmlAsync_WritesASelfContainedFile_AndReturnsStatus()
    {
        var dir = Directory.CreateTempSubdirectory("sv-export");
        try
        {
            var svc = Create();
            var tab = DocumentTabViewModel.FromFile("# Hi\n\ntext", "/docs/d.md");
            var target = Path.Combine(dir.FullName, "out.html");

            var status = await svc.ExportHtmlAsync(tab, target);

            Assert.True(File.Exists(target));
            var html = File.ReadAllText(target);
            Assert.Contains("<!DOCTYPE html>", html);
            Assert.Contains("<h1", html);
            Assert.StartsWith("Экспортировано", status);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CopyAsRichTextAsync_PutsHtmlAndAMarkdownFallbackOnTheClipboard()
    {
        var clip = new FakeClipboardService();
        var svc = Create(clipboard: clip);
        var tab = DocumentTabViewModel.FromFile("# Hi", "/docs/d.md");

        var status = await svc.CopyAsRichTextAsync(tab);

        Assert.Contains("<h1", clip.LastHtml);
        Assert.Equal("# Hi", clip.LastHtmlPlainFallback); // raw markdown is the plain fallback
        Assert.StartsWith("Скопировано", status);
    }

    [Fact]
    public async Task PrintViaBrowserAsync_WritesTempHtml_AndOpensIt()
    {
        var shell = new FakeShellService();
        var svc = Create(shell: shell);
        var tab = DocumentTabViewModel.FromFile("# Hi", "/docs/d.md");

        var status = await svc.PrintViaBrowserAsync(tab);

        Assert.Single(shell.Opened);
        var opened = shell.Opened[0];
        Assert.EndsWith(".print.html", opened);
        Assert.True(File.Exists(opened));
        Assert.StartsWith("Открыто в браузере", status);

        // best-effort: clean up the temp dir the test created (the service's own cleanup is delayed)
        var dir = Path.GetDirectoryName(opened);
        if (dir is not null && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
