using System.IO;
using SeriousView.Core.Documents;
using SeriousView.Core.Text;
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

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
        Assert.False(vm.ShowSource);
    }

    [Fact]
    public void ViewModeToggleTip_NamesTheSwitchTarget()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");
        Assert.Equal("Показать исходник", vm.ViewModeToggleTip);     // in preview → click switches to source

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal("Показать предпросмотр", vm.ViewModeToggleTip);  // in source → click switches to preview
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

    [Fact]
    public void Outline_ForMarkdownWithHeadings_IsPopulated()
    {
        var vm = DocumentTabViewModel.FromFile("# First\n\ntext\n\n## Second", "/docs/readme.md");

        Assert.True(vm.HasOutline);
        Assert.Equal(2, vm.Outline.Count);
        Assert.Equal("First", vm.Outline[0].Text);
        Assert.Equal("Second", vm.Outline[1].Text);
    }

    [Fact]
    public void Outline_ForCodeFile_IsEmpty()
    {
        // A '#'-prefixed line in a non-markdown file must not produce an outline.
        var vm = DocumentTabViewModel.FromFile("# not markdown", "/src/a.cs");

        Assert.False(vm.HasOutline);
        Assert.Empty(vm.Outline);
    }

    [Fact]
    public void NavigateToHeading_RaisesNavigationRequested_WithTheHeading()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n## B", "/docs/readme.md");
        HeadingOutline? received = null;
        vm.NavigationRequested += h => received = h;

        var target = vm.Outline[1];
        vm.NavigateToHeadingCommand.Execute(target);

        Assert.Same(target, received);
    }

    [Fact]
    public void FromLoad_Binary_ShowsNotice_HidesEditorAndPreview()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Binary(2048), "/img/pic.png");

        Assert.True(vm.ShowNotice);
        Assert.False(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        Assert.Contains("Бинарный", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_TooLarge_ShowsNotice()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.TooLarge(60L * 1024 * 1024), "/big.txt");

        Assert.True(vm.ShowNotice);
        Assert.Contains("слишком большой", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_EmptyFile_ShowsNotice()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.ForText("", "UTF-8", "", 0), "/empty.md");

        Assert.True(vm.ShowNotice);
        Assert.Equal("Файл пуст", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_Text_StatusShowsEncodingAndEol()
    {
        var vm = DocumentTabViewModel.FromLoad(
            FileLoadResult.ForText("a\nb", "Windows-1251", "LF", 3), "/doc.txt");

        Assert.False(vm.ShowNotice);
        Assert.Contains("Windows-1251", vm.StatusText);
        Assert.Contains("LF", vm.StatusText);
    }

    [Fact]
    public void FromLoad_BigText_SuppressesHighlight()
    {
        var vm = DocumentTabViewModel.FromLoad(
            FileLoadResult.ForText("x", "UTF-8", "LF", 10L * 1024 * 1024), "/big.cs");

        Assert.True(vm.HighlightSuppressed);
        Assert.Contains("без подсветки", vm.StatusText);
    }

    [Fact]
    public void CaretText_FormatsLineAndColumn_AndUpdates()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb", "/src/a.cs");
        Assert.Equal("Стр 1, Кол 1", vm.CaretText);

        vm.CaretLine = 5;
        vm.CaretColumn = 12;
        Assert.Equal("Стр 5, Кол 12", vm.CaretText);
    }

    [Fact]
    public void SubmitGoToLine_ValidLine_RaisesRequest_AndCloses()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb\nc\nd\ne", "/src/a.cs");
        vm.IsGoToLineOpen = true;
        vm.GoToLineText = "3";
        int? got = null;
        vm.GoToLineRequested += l => got = l;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.Equal(3, got);
        Assert.False(vm.IsGoToLineOpen);
        Assert.Equal("", vm.GoToLineText);
    }

    [Fact]
    public void SubmitGoToLine_ClampsToDocumentBounds()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb\nc", "/src/a.cs"); // 3 lines
        vm.GoToLineText = "999";
        int? got = null;
        vm.GoToLineRequested += l => got = l;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.Equal(3, got);
    }

    [Fact]
    public void SubmitGoToLine_NonNumeric_DoesNotRaise_ButCloses()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb", "/src/a.cs");
        vm.IsGoToLineOpen = true;
        vm.GoToLineText = "abc";
        var raised = false;
        vm.GoToLineRequested += _ => raised = true;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.False(raised);
        Assert.False(vm.IsGoToLineOpen);
    }
}
