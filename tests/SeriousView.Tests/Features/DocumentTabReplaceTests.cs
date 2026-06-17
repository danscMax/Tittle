using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

public class DocumentTabReplaceTests
{
    // FakeEditorActions lives in the parent SeriousView.Tests namespace (visible by enclosing-namespace lookup).
    private static (DocumentTabViewModel Vm, FakeEditorActions Editor) Open(string text)
    {
        var vm = DocumentTabViewModel.FromFile(text, "/x.txt");
        var editor = new FakeEditorActions(text);
        vm.EditorActions = editor; // mirrors what DocumentView wires on attach
        return (vm, editor);
    }

    [Fact]
    public void ReplaceAll_ReplacesEveryMatch_OnLiveEditorText()
    {
        var (vm, editor) = Open("foo bar foo");
        vm.SearchQuery = "foo";
        vm.ReplaceText = "X";

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("X bar X", editor.Text);
        Assert.Equal(0, vm.SearchMatchCount); // re-scan of the live text → no matches left
    }

    [Fact]
    public void ReplaceAll_Regex_GroupSubstitution()
    {
        var (vm, editor) = Open("a1 b2");
        vm.SearchRegex = true;
        vm.SearchQuery = "([a-z])([0-9])";
        vm.ReplaceText = "$2$1";

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("1a 2b", editor.Text);
    }

    [Fact]
    public void ReplaceAll_NoMatch_LeavesEditorUntouched()
    {
        var (vm, editor) = Open("hello");
        vm.SearchQuery = "zzz";
        vm.ReplaceText = "X";

        vm.ReplaceAllCommand.Execute(null);

        Assert.Equal("hello", editor.Text);
        Assert.Equal(0, editor.ReplaceCalls);
    }

    [Fact]
    public void ReplaceCurrent_ReplacesFirstMatch()
    {
        var (vm, editor) = Open("a a a");
        vm.SearchQuery = "a"; // three matches; current index 0
        vm.ReplaceText = "b";

        vm.ReplaceCurrentCommand.Execute(null);

        Assert.Equal("b a a", editor.Text);
    }

    [Fact]
    public void OpenReplace_ShowsReplaceRow_OpenSearchHidesIt()
    {
        var (vm, _) = Open("text");

        vm.OpenReplaceCommand.Execute(null);
        Assert.True(vm.IsReplaceMode);
        Assert.True(vm.IsSearchOpen);

        vm.OpenSearchCommand.Execute(null);
        Assert.False(vm.IsReplaceMode);
    }
}
