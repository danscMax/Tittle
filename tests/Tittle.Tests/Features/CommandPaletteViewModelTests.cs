using System;
using CommunityToolkit.Mvvm.Input;
using Tittle.Features.Palette;
using Xunit;

namespace Tittle.Tests.Features;

public class CommandPaletteViewModelTests
{
    private static PaletteItem Item(string title, Action run) => new(title, new RelayCommand(run));

    [Fact]
    public void Query_FiltersToSubsequenceMatches()
    {
        var vm = new CommandPaletteViewModel(new[]
        {
            Item("Open File", () => { }),
            Item("Close Tab", () => { }),
            Item("Open Sample", () => { }),
        });

        vm.Query = "opfil"; // only "Open File" is a subsequence

        Assert.Single(vm.Results);
        Assert.Equal("Open File", vm.Results[0].Title);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void EmptyQuery_ShowsAll_InOriginalOrder()
    {
        var vm = new CommandPaletteViewModel(new[] { Item("Alpha", () => { }), Item("Beta", () => { }) });

        Assert.Equal(2, vm.Results.Count);
        Assert.Equal("Alpha", vm.Results[0].Title);
    }

    [Fact]
    public void MoveSelection_WrapsAround()
    {
        var vm = new CommandPaletteViewModel(new[] { Item("A", () => { }), Item("B", () => { }) });

        Assert.Equal(0, vm.SelectedIndex);
        vm.MoveSelection(-1);
        Assert.Equal(1, vm.SelectedIndex); // wraps to the last
        vm.MoveSelection(1);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void Execute_RunsSelectedCommand_AndRaisesClosed()
    {
        var ran = "";
        var vm = new CommandPaletteViewModel(new[]
        {
            Item("First", () => ran = "First"),
            Item("Second", () => ran = "Second"),
        });
        var closed = false;
        vm.Closed += () => closed = true;

        vm.MoveSelection(1); // select "Second"
        vm.Execute();

        Assert.Equal("Second", ran);
        Assert.True(closed);
    }
}
