using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Styling;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace SeriousView.Features.Viewer;

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

    /// <summary>When true, TextMate highlighting is not installed (used for very large files,
    /// where tokenization would stall — the file is shown as plain text).</summary>
    public static readonly AttachedProperty<bool> SuppressHighlightProperty =
        AvaloniaProperty.RegisterAttached<TextEditor, bool>("SuppressHighlight", typeof(EditorBehavior));

    public static void SetSuppressHighlight(TextEditor editor, bool value)
        => editor.SetValue(SuppressHighlightProperty, value);

    public static bool GetSuppressHighlight(TextEditor editor)
        => editor.GetValue(SuppressHighlightProperty);

    // Per-editor TextMate state; weak keys so editors can be GC'd.
    private static readonly ConditionalWeakTable<TextEditor, EditorState> States = new();

    // SERIOUSVIEW_NOTM disables TextMate entirely (RAM isolation measurement).
    private static readonly bool TextMateDisabled =
        Environment.GetEnvironmentVariable("SERIOUSVIEW_NOTM") is not null;

    static EditorBehavior()
    {
        TextProperty.Changed.AddClassHandler<TextEditor>(OnTextChanged);
        GrammarExtensionProperty.Changed.AddClassHandler<TextEditor>(OnGrammarExtensionChanged);
        SuppressHighlightProperty.Changed.AddClassHandler<TextEditor>(OnSuppressHighlightChanged);
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
        if (TextMateDisabled || GetSuppressHighlight(editor))
            return;

        var state = EnsureInstalled(editor);
        ApplyGrammar(state, e.GetNewValue<string?>());
    }

    // Highlighting can be suppressed (large files) independently of the grammar binding, and the
    // two attached properties can apply in either order — handle both: tear down when turned on,
    // (re)install when turned off.
    private static void OnSuppressHighlightChanged(TextEditor editor, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            if (States.TryGetValue(editor, out var state))
            {
                state.Installation.Dispose();
                States.Remove(editor);
            }
        }
        else if (!TextMateDisabled)
        {
            var state = EnsureInstalled(editor);
            ApplyGrammar(state, GetGrammarExtension(editor));
        }
    }

    private static EditorState EnsureInstalled(TextEditor editor)
    {
        if (States.TryGetValue(editor, out var existing))
            return existing;

        var registry = new RegistryOptions(PickTheme(editor));
        var installation = editor.InstallTextMate(registry);
        var state = new EditorState(registry, installation);
        States.Add(editor, state);
        editor.DetachedFromVisualTree += OnDetached;
        editor.ActualThemeVariantChanged += OnThemeVariantChanged;
        return state;
    }

    // VS Code "Light+"/"Dark+" to match the app theme.
    private static ThemeName PickTheme(TextEditor editor)
        => editor.ActualThemeVariant == ThemeVariant.Light ? ThemeName.LightPlus : ThemeName.DarkPlus;

    private static void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        if (sender is not TextEditor editor || !States.TryGetValue(editor, out var state))
            return;

        // AvaloniaEdit.TextMate's SetTheme refreshes token colours but leaves the editor's
        // own background/foreground on the theme it was first installed with. Reinstall so the
        // new theme applies fully (editor surface follows Light+/Dark+ with the app theme).
        var grammar = GetGrammarExtension(editor);
        state.Installation.Dispose();
        States.Remove(editor);

        var registry = new RegistryOptions(PickTheme(editor));
        var fresh = new EditorState(registry, editor.InstallTextMate(registry));
        States.Add(editor, fresh);
        ApplyGrammar(fresh, grammar);
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextEditor editor || !States.TryGetValue(editor, out var state))
            return;

        editor.DetachedFromVisualTree -= OnDetached;
        editor.ActualThemeVariantChanged -= OnThemeVariantChanged;
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
