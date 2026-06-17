using System.Collections.Generic;
using System.Linq;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    private static Macro MWithShortcut(string name, string shortcut) =>
        new(name, RepeatMode.Once, 1, new IEditorIntent[] { new InsertTextIntent("x") }, shortcut);

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

    [Fact]
    public void Build_CarriesShortcut()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);

        vm.Rows[0].Shortcut = "Ctrl+Shift+M";
        vm.Commit();

        Assert.Equal("Ctrl+Shift+M", lib.Saved[0].Shortcut);
    }

    [Fact]
    public void Capture_AssignsGesture_AndPersists()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);

        vm.BeginCaptureCommand.Execute(vm.Rows[0]);
        Assert.True(vm.IsCapturing);
        Assert.True(vm.Rows[0].Capturing);

        vm.ApplyCapturedShortcut("Alt+M");

        Assert.False(vm.IsCapturing);
        Assert.False(vm.Rows[0].Capturing);
        Assert.Equal("Alt+M", vm.Rows[0].Shortcut);
        Assert.Equal("Alt+M", lib.Saved[0].Shortcut); // committed on capture
    }

    [Fact]
    public void Capture_Cancel_LeavesShortcutUnchanged()
    {
        var lib = new FakeMacroLibrary(MWithShortcut("A", "Ctrl+1"));
        var vm = new MacroManagerViewModel(lib);

        vm.BeginCaptureCommand.Execute(vm.Rows[0]);
        vm.ApplyCapturedShortcut(null); // Esc / cancel

        Assert.False(vm.IsCapturing);
        Assert.False(vm.Rows[0].Capturing);
        Assert.Equal("Ctrl+1", vm.Rows[0].Shortcut);
    }

    [Fact]
    public void ClearShortcut_UnbindsAndPersists()
    {
        var lib = new FakeMacroLibrary(MWithShortcut("A", "Ctrl+Shift+M"));
        var vm = new MacroManagerViewModel(lib);

        vm.ClearShortcutCommand.Execute(vm.Rows[0]);

        Assert.Null(vm.Rows[0].Shortcut);
        Assert.Null(lib.Saved[0].Shortcut);
    }

    // Exercises MacroManagerWindow.OnCaptureKeyDown end-to-end: a Ctrl-modified key while capturing becomes
    // the row's gesture. The window isn't Show()n — glyph-bearing modals don't render headless (see
    // ModalWindowTests) — but the tunnel KeyDown handler is registered on the window, so RaiseEvent routes
    // to it without a render pass. InitializeComponent (in the ctor) also loads the dialog XAML.
    [AvaloniaFact]
    public void Window_CaptureKeyDown_AssignsCtrlModifiedGesture()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);
        var window = new MacroManagerWindow { DataContext = vm };

        vm.BeginCaptureCommand.Execute(vm.Rows[0]);
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.M,
            KeyModifiers = KeyModifiers.Control,
        });

        Assert.Equal("Ctrl+M", vm.Rows[0].Shortcut);
        Assert.Equal("Ctrl+M", lib.Saved[0].Shortcut);
    }

    // A bare key (no Ctrl/Alt) must NOT be accepted while capturing — it would shadow plain typing.
    [AvaloniaFact]
    public void Window_CaptureKeyDown_IgnoresBareKey()
    {
        var lib = new FakeMacroLibrary(M("A"));
        var vm = new MacroManagerViewModel(lib);
        var window = new MacroManagerWindow { DataContext = vm };

        vm.BeginCaptureCommand.Execute(vm.Rows[0]);
        window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.M,
            KeyModifiers = KeyModifiers.None,
        });

        Assert.True(vm.IsCapturing);       // still waiting
        Assert.Null(vm.Rows[0].Shortcut);
    }
}
