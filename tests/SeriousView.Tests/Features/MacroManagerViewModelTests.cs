using System.Collections.Generic;
using System.Linq;
using SeriousView.Core.Editing;
using SeriousView.Features.Macros;
using Xunit;

namespace SeriousView.Tests.Features;

public class MacroManagerViewModelTests
{
    private sealed class FakeMacroLibrary : IMacroLibrary
    {
        public List<Macro> Saved;
        public readonly List<Macro> Played = new();

        public FakeMacroLibrary(params Macro[] macros) => Saved = macros.ToList();

        public IReadOnlyList<Macro> Macros => Saved;
        public void ReplaceMacroLibrary(IReadOnlyList<Macro> macros) => Saved = macros.ToList();
        public void ReplayMacro(Macro macro) => Played.Add(macro);
    }

    private static Macro M(string name, int count = 1) =>
        new(name, RepeatMode.Once, count, new IEditorIntent[] { new InsertTextIntent("x") });

    [Fact]
    public void Rows_AreBuiltFromTheLibrary()
    {
        var vm = new MacroManagerViewModel(new FakeMacroLibrary(M("A"), M("B")));

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("A", vm.Rows[0].Name);
        Assert.Equal(1, vm.Rows[0].StepCount);
    }

    [Fact]
    public void Delete_RemovesTheRow_AndCommitsToTheLibrary()
    {
        var lib = new FakeMacroLibrary(M("A"), M("B"));
        var vm = new MacroManagerViewModel(lib);

        vm.DeleteCommand.Execute(vm.Rows[0]);

        Assert.Single(vm.Rows);
        Assert.Single(lib.Saved);
        Assert.Equal("B", lib.Saved[0].Name);
    }

    [Fact]
    public void Rename_ThenCommit_PersistsTheNewName()
    {
        var lib = new FakeMacroLibrary(M("Old"));
        var vm = new MacroManagerViewModel(lib);

        vm.Rows[0].Name = "New";
        vm.Commit();

        Assert.Equal("New", lib.Saved[0].Name);
    }

    [Fact]
    public void PlayTimes_ReplaysWithTimesModeAndCount()
    {
        var lib = new FakeMacroLibrary(M("A", count: 5));
        var vm = new MacroManagerViewModel(lib);

        vm.PlayTimesCommand.Execute(vm.Rows[0]);

        Assert.Single(lib.Played);
        Assert.Equal(RepeatMode.Times, lib.Played[0].Mode);
        Assert.Equal(5, lib.Played[0].Count);
    }

    [Fact]
    public void PlayToEnd_ReplaysWithUntilNoMatchMode()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);

        vm.PlayToEndCommand.Execute(vm.Rows[0]);

        Assert.Equal(RepeatMode.UntilNoMatch, lib.Played[0].Mode);
    }

    [Fact]
    public void BlankName_CommitsAsAPlaceholder()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);

        vm.Rows[0].Name = "   ";
        vm.Commit();

        Assert.Equal("Без имени", lib.Saved[0].Name);
    }
}
