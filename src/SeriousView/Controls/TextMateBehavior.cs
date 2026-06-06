using System;
using System.Runtime.CompilerServices;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace SeriousView.Controls;

/// <summary>
/// Attaches TextMate syntax highlighting to a <see cref="TextEditor"/> and switches
/// grammar by file extension — driven from a view model via the
/// <c>GrammarExtension</c> attached property.
///
/// TextMate is a per-control concern (not VM-bindable), so each editor owns its own
/// <see cref="TextMate.Installation"/>. The installation is <see cref="IDisposable"/>
/// and is released on <see cref="Visual.DetachedFromVisualTree"/> to avoid leaking
/// native TextMateSharp state when tabs close.
/// </summary>
public static class TextMateBehavior
{
    /// <summary>File extension (e.g. ".cs", ".md") whose grammar should be applied.</summary>
    public static readonly AttachedProperty<string?> GrammarExtensionProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>(
            "GrammarExtension", typeof(TextMateBehavior));

    public static void SetGrammarExtension(TextEditor editor, string? value)
        => editor.SetValue(GrammarExtensionProperty, value);

    public static string? GetGrammarExtension(TextEditor editor)
        => editor.GetValue(GrammarExtensionProperty);

    // Per-editor TextMate state; weak keys so editors can be GC'd.
    private static readonly ConditionalWeakTable<TextEditor, EditorState> States = new();

    // SERIOUSVIEW_NOTM disables TextMate entirely (RAM isolation measurement).
    private static readonly bool Disabled =
        Environment.GetEnvironmentVariable("SERIOUSVIEW_NOTM") is not null;

    static TextMateBehavior()
    {
        GrammarExtensionProperty.Changed.AddClassHandler<TextEditor>(OnGrammarExtensionChanged);
    }

    private static void OnGrammarExtensionChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (Disabled)
            return;

        var state = EnsureInstalled(editor);
        ApplyGrammar(state, e.GetNewValue<string?>());
    }

    private static EditorState EnsureInstalled(TextEditor editor)
    {
        if (States.TryGetValue(editor, out var existing))
            return existing;

        var registry = new RegistryOptions(ThemeName.DarkPlus);
        var installation = editor.InstallTextMate(registry);
        var state = new EditorState(registry, installation);
        States.Add(editor, state);
        editor.DetachedFromVisualTree += OnDetached;
        return state;
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextEditor editor || !States.TryGetValue(editor, out var state))
            return;

        editor.DetachedFromVisualTree -= OnDetached;
        state.Installation.Dispose();
        States.Remove(editor);
    }

    private static void ApplyGrammar(EditorState state, string? extension)
    {
        if (string.IsNullOrEmpty(extension))
            return;

        try
        {
            var language = state.Registry.GetLanguageByExtension(extension);
            if (language is not null)
                state.Installation.SetGrammar(state.Registry.GetScopeByLanguageId(language.Id));
        }
        catch
        {
            // No grammar for this extension — leave as plain text.
        }
    }

    private sealed record EditorState(RegistryOptions Registry, TextMate.Installation Installation);
}
