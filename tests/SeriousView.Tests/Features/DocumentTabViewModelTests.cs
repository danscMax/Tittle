using System.IO;
using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

public class DocumentTabViewModelTests
{
    [Fact]
    public void FromFile_MarkdownFile_DefaultsToPreview()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");

        Assert.True(vm.IsMarkdown);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
        Assert.False(vm.ShowSource);
        Assert.Equal("Предпросмотр", vm.ViewModeLabel);
        Assert.True(vm.ToggleViewModeCommand.CanExecute(null));
    }

    [Fact]
    public void FromFile_CodeFile_IsSourceOnly()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.False(vm.IsMarkdown);
        Assert.True(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        Assert.False(vm.ToggleViewModeCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleViewMode_FlipsPreviewSource_ForMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Source, vm.ViewMode);
        Assert.True(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        Assert.Equal("Исходник", vm.ViewModeLabel);

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
        Assert.False(vm.ShowSource);
        Assert.Equal("Предпросмотр", vm.ViewModeLabel);
    }

    [Fact]
    public void AssetPathRoot_IsDocumentDirectory()
    {
        const string path = "/docs/sub/readme.md";
        var vm = DocumentTabViewModel.FromFile("# Title", path);

        Assert.Equal(Path.GetDirectoryName(path), vm.AssetPathRoot);
    }

    [Fact]
    public void AssetPathRoot_Null_WhenNoFilePath()
    {
        var vm = DocumentTabViewModel.CreateSample();

        Assert.Null(vm.AssetPathRoot);
    }

    [Fact]
    public void PreviewMarkdown_PassesPlainTextThrough()
    {
        const string text = "# Title\n\nPlain paragraph with **bold**.";
        var vm = DocumentTabViewModel.FromFile(text, "/docs/readme.md");

        Assert.Equal(text, vm.PreviewMarkdown);
    }

    [Fact]
    public void PreviewMarkdown_Empty_ForNonMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.Equal("", vm.PreviewMarkdown);
    }
}
