using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using SeriousView.ViewModels;
using Xunit;

namespace SeriousView.Tests.ViewModels;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateVm(
        string? dialogPath = null, string content = "a\nb\nc", string[]? args = null)
        => new(
            new FakeFileDialogService(dialogPath),
            new FakeFileReader(content),
            new FakeThemeService(),
            args ?? Array.Empty<string>());

    [AvaloniaFact]
    public void Startup_WithoutArgs_OpensSampleTab()
    {
        var vm = CreateVm();

        Assert.Single(vm.Tabs);
        Assert.Equal("Пример", vm.SelectedTab!.Header);
    }

    [AvaloniaFact]
    public void Startup_WithFileArg_OpensFileTab()
    {
        var vm = CreateVm(content: "x\ny", args: new[] { "/path/to/sample.cs" });

        Assert.Single(vm.Tabs);
        Assert.Equal("sample.cs", vm.SelectedTab!.Header);
        Assert.Equal(".cs", vm.SelectedTab.GrammarExtension);
        Assert.Equal("x\ny", vm.SelectedTab.Content);
    }

    [AvaloniaFact]
    public async Task OpenFile_AddsTabActivatesIt_AndUpdatesStatus()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md", content: "one\ntwo");

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal("doc.md", vm.SelectedTab!.Header);
        Assert.Equal(".md", vm.SelectedTab.GrammarExtension);
        Assert.Contains("Строк", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task OpenFile_WhenCancelled_DoesNotAddTab()
    {
        var vm = CreateVm(dialogPath: null);

        await vm.OpenFileCommand.ExecuteAsync(null);

        Assert.Single(vm.Tabs);
    }

    [AvaloniaFact]
    public async Task CloseTab_RemovesTab_AndSelectsNeighbour()
    {
        var vm = CreateVm(dialogPath: "/path/doc.md");
        await vm.OpenFileCommand.ExecuteAsync(null); // now 2 tabs, doc.md active
        var closing = vm.SelectedTab!;

        vm.CloseTabCommand.Execute(closing);

        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.SelectedTab);
        Assert.NotSame(closing, vm.SelectedTab);
    }

    [AvaloniaFact]
    public void ToggleTheme_InvokesThemeService()
    {
        var theme = new FakeThemeService();
        var vm = new MainWindowViewModel(
            new FakeFileDialogService(null), new FakeFileReader("x"), theme, Array.Empty<string>());

        vm.ToggleThemeCommand.Execute(null);

        Assert.Equal(1, theme.ToggleCount);
    }
}
