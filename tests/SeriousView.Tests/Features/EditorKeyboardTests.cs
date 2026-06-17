using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Xunit;

namespace SeriousView.Tests.Features;

// Empirical checks of AvaloniaEdit 11.4.1 keyboard behavior under headless input, settling the
// editing/macro questions that were once deferred as "needs a live GUI". They double as regression
// guards: if a future AvaloniaEdit changes any of these, a test flips and tells us to revisit the
// macro tap (e.g. Tab starting to raise TextEntered would double-record against the key-tunnel path).
public class EditorKeyboardTests
{
    private static (Window window, TextEditor editor) ShowEditor(string text)
    {
        var editor = new TextEditor { Text = text };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        editor.TextArea.Focus();
        return (window, editor);
    }

    // Sanity: a real text-input event reaches the focused editor and raises TextEntered. This is the path
    // ordinary typing takes in the app (and what DocumentView.OnEditorTextEntered records as InsertText).
    [AvaloniaFact]
    public void TextInput_RaisesTextEntered()
    {
        var (window, editor) = ShowEditor("");
        string? entered = null;
        editor.TextArea.TextEntered += (_, e) => entered = e.Text;

        window.KeyTextInput("x");

        Assert.Equal("x", entered);
    }

    // Enter DOES raise TextEntered → the typing tap already records it as InsertText("\n"); no key-tunnel
    // entry is needed (and adding one would double-record).
    [AvaloniaFact]
    public void Enter_RaisesTextEntered()
    {
        var (window, editor) = ShowEditor("ab");
        editor.CaretOffset = 2;
        string? entered = null;
        editor.TextArea.TextEntered += (_, e) => entered = e.Text;

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.False(string.IsNullOrEmpty(entered),
            $"Enter TextEntered='{Show(entered)}', docLen={editor.Text.Length}");
    }

    [AvaloniaFact]
    public void Enter_InsertsNewline()
    {
        var (window, editor) = ShowEditor("ab");
        editor.CaretOffset = 2;

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(editor.Text.Length > 2, $"Enter should insert a newline; doc='{Show(editor.Text)}'");
    }

    // Tab inserts a "\t" but does NOT raise TextEntered → the typing tap misses it, which is exactly why
    // the key tunnel records Tab as InsertText("\t") (see MainWindow.MacroKeyIntent / MacroKeyIntentTests).
    [AvaloniaFact]
    public void Tab_InsertsTab_ButDoesNotRaiseTextEntered()
    {
        var (window, editor) = ShowEditor("ab");
        editor.CaretOffset = 2;
        string? entered = null;
        editor.TextArea.TextEntered += (_, e) => entered = e.Text;

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.True(string.IsNullOrEmpty(entered),
            $"Tab unexpectedly raised TextEntered='{Show(entered)}' — macro recording would double-count it");
        Assert.Equal("ab\t", editor.Text);
    }

    // Alt+Shift+Arrow produces a RectangleSelection out of the box → keyboard column-select already works;
    // nothing custom to implement (this proves the previously "unconfirmed on 11.4" deferral is resolved).
    [AvaloniaFact]
    public void AltShiftRight_MakesRectangleSelection()
    {
        var (window, editor) = ShowEditor("abc\ndef\nghi");
        editor.CaretOffset = 0;

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Alt | RawInputModifiers.Shift);

        Assert.True(editor.TextArea.Selection is RectangleSelection,
            $"selection type={editor.TextArea.Selection.GetType().Name}, len={editor.TextArea.Selection.Length}");
    }

    private static string Show(string? s) =>
        s is null ? "<null>" : s.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
