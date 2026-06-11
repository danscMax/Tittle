using System;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

public class EditorBehaviorTests
{
    // Opening a file should show line 1 — assigning the document used to leave the caret at the end,
    // so the status bar read the last line and the first ↓ jumped to the bottom.
    [AvaloniaFact]
    public void SetText_LeavesCaretAtStart()
    {
        var editor = new TextEditor();

        EditorBehavior.SetText(editor, "one\ntwo\nthree");

        Assert.Equal(0, editor.CaretOffset);
    }

    // B3: a theme switch reinstalls TextMate. If InstallTextMate throws between disposing the old
    // installation and registering the new one, the editor must NOT be left with no entry while a
    // native installation leaks. The fix builds the fresh installation BEFORE touching the table,
    // so a throw leaves the original (working) state in place — disposable, not orphaned.
    [AvaloniaFact]
    public void FailedThemeReinstall_KeepsStateConsistent_NoOrphan()
    {
        var original = EditorBehavior.InstallTextMate;
        try
        {
            // First install (real) so the editor has a registered state for the theme switch.
            var editor = new TextEditor();
            EditorBehavior.SetGrammarExtension(editor, ".cs");
            Assert.True(EditorBehavior.HasState(editor), "expected a state after first install");

            // Now force the reinstall step to throw, simulating InstallTextMate / GetScopeByLanguageId
            // failing mid theme switch.
            EditorBehavior.InstallTextMate = (_, _) => throw new InvalidOperationException("boom");

            // Must not throw out of the handler, and must leave the editor with a VALID state
            // (the original one) — never empty-with-a-leaked-installation.
            EditorBehavior.RaiseThemeVariantChanged(editor);

            Assert.True(
                EditorBehavior.HasState(editor),
                "failed reinstall must keep the original valid state, not leave the editor empty");

            // The retained state must still be disposable on detach — i.e. no orphaned handle.
            // Teardown() (run on DetachedFromVisualTree) must find and dispose the state.
            EditorBehavior.RaiseTeardown(editor);
            Assert.False(
                EditorBehavior.HasState(editor),
                "Teardown must dispose and remove the retained state — nothing orphaned");
        }
        finally
        {
            EditorBehavior.InstallTextMate = original;
        }
    }
}
