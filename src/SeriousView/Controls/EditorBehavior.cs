using System;
using System.Runtime.CompilerServices;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace SeriousView.Controls;

/// <summary>
/// Bridges a <see cref="TextEditor"/> to a view model via attached properties:
/// <c>Text</c> (document content) and <c>GrammarExtension</c> (TextMate highlighting).
///
/// <para><b>Why not bind <c>TextEditor.Document</c> directly?</b> AvaloniaEdit manages
/// its document internally, so declarative <c>Document="{Binding}"</c> does not render
/// content. We instead set <see cref="TextEditor.Text"/> imperatively when the bound
/// <c>Text</c> attached property changes (one-way VM → editor; two-way editing arrives
/// with M5).</para>
///
/// <para>TextMate is a per-control concern: each editor owns its
/// <see cref="TextMate.Installation"/>, disposed on
/// <see cref="Visual.DetachedFromVisualTree"/> to avoid leaking native state.</para>
/// </summary>
public static class EditorBehavior
{
    /// <summary>Document content, pushed into the editor one-way.</summary>
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("Text", typeof(EditorBehavior));

    public static void SetText(TextEditor editor, string? value) => editor.SetValue(TextProperty, value);
    public static string? GetText(TextEditor editor) => editor.GetValue(TextProperty);

    /// <summary>File extension (e.g. ".cs", ".md") whose grammar should be applied.</summary>
    public static readonly AttachedProperty<string?> GrammarExtensionProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, string?>("GrammarExtension", typeof(EditorBehavior));

    public static void SetGrammarExtension(TextEditor editor, string? value)
        => editor.SetValue(GrammarExtensionProperty, value);

    public static string? GetGrammarExtension(TextEditor editor)
        => editor.GetValue(GrammarExtensionProperty);

    // Per-editor TextMate state; weak keys so editors can be GC'd.
    private static readonly ConditionalWeakTable<TextEditor, EditorState> States = new();

    // SERIOUSVIEW_NOTM disables TextMate entirely (RAM isolation measurement).
    private static readonly bool TextMateDisabled =
        Environment.GetEnvironmentVariable("SERIOUSVIEW_NOTM") is not null;

    static EditorBehavior()
    {
        TextProperty.Changed.AddClassHandler<TextEditor>(OnTextChanged);
        GrammarExtensionProperty.Changed.AddClassHandler<TextEditor>(OnGrammarExtensionChanged);
    }

    private static void OnTextChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        var text = e.GetNewValue<string?>() ?? string.Empty;
        // Drive the Document directly (the editor's Text setter throws if Document is null).
        if (editor.Document is null)
            editor.Document = new TextDocument(text);
        else if (editor.Document.Text != text)
            editor.Document.Text = text;
    }

    private static void OnGrammarExtensionChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (TextMateDisabled)
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
