using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Text;
using SeriousView.Shared;

namespace SeriousView.Features.Shell;

/// <summary>How a markdown document is shown: rendered preview or raw source.
/// Non-markdown files are always source (the toggle is hidden).</summary>
public enum DocumentViewMode
{
    Preview,
    Source,
}

/// <summary>One open document = one tab. Holds its content, grammar, header and
/// per-document status metrics. Content is pushed to the editor via
/// <c>EditorBehavior.Text</c> (AvaloniaEdit can't bind Document directly).</summary>
public partial class DocumentTabViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _header;

    [ObservableProperty]
    private string? _filePath;

    /// <summary>Extension (e.g. ".cs") driving TextMate grammar in the View.</summary>
    [ObservableProperty]
    private string? _grammarExtension;

    /// <summary>Per-document status line (lines / chars), surfaced in the status bar.</summary>
    [ObservableProperty]
    private string _statusText = "";

    /// <summary>Document text, bound one-way into the editor. Named DocumentText (not
    /// Content) to avoid colliding with TabViewItem.Content when bound inside a TabView.</summary>
    public string DocumentText { get; }

    /// <summary>True for markdown files — drives whether a rendered preview is offered.</summary>
    public bool IsMarkdown => MarkdownFile.IsMarkdownExtension(GrammarExtension);

    /// <summary>Preview vs source. Defaults to Preview; only meaningful for markdown
    /// (for code files <see cref="ShowSource"/> short-circuits to true regardless).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ViewModeLabel))]
    private DocumentViewMode _viewMode = DocumentViewMode.Preview;

    /// <summary>Show the rendered markdown preview (markdown file in Preview mode).</summary>
    public bool ShowPreview => IsMarkdown && ViewMode == DocumentViewMode.Preview;

    /// <summary>Show the source editor (any non-markdown file, or markdown in Source mode).</summary>
    public bool ShowSource => !IsMarkdown || ViewMode == DocumentViewMode.Source;

    /// <summary>Base directory for resolving relative image/asset paths in the preview.</summary>
    public string? AssetPathRoot => FilePath is null ? null : Path.GetDirectoryName(FilePath);

    private string? _previewMarkdown;

    /// <summary>Markdown handed to the renderer — empty for non-markdown files so the
    /// (hidden) preview never parses code as markdown. Run through the Core preprocessor
    /// (admonitions, task lists). Cached: the document text is immutable.</summary>
    public string PreviewMarkdown =>
        _previewMarkdown ??= IsMarkdown ? MarkdownPreprocessor.Transform(DocumentText) : "";

    /// <summary>Label for the preview/source toggle, reflecting the current mode.</summary>
    public string ViewModeLabel => ViewMode == DocumentViewMode.Preview ? "Предпросмотр" : "Исходник";

    /// <summary>Flip preview ⟷ source. Only enabled for markdown documents.</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ToggleViewMode()
        => ViewMode = ViewMode == DocumentViewMode.Preview ? DocumentViewMode.Source : DocumentViewMode.Preview;

    private DocumentTabViewModel(string header, string content)
    {
        _header = header;
        DocumentText = content;
    }

    public static DocumentTabViewModel FromFile(string text, string path)
    {
        var tab = new DocumentTabViewModel(Path.GetFileName(path), text)
        {
            FilePath = path,
            GrammarExtension = Path.GetExtension(path),
        };
        tab.StatusText = $"Строк: {TextMetrics.LineCount(text)}   ·   Символов: {TextMetrics.CharCount(text)}";
        return tab;
    }

    public static DocumentTabViewModel CreateSample()
    {
        var tab = new DocumentTabViewModel("Пример", Sample) { GrammarExtension = ".cs" };
        tab.StatusText = $"Строк: {TextMetrics.LineCount(Sample)}   ·   подсветка: C#";
        return tab;
    }

    private const string Sample = @"// SeriousView — нативный markdown/code viewer
// Движок: Avalonia 11 + AvaloniaEdit (TextMate / Dark+)
using System;
using System.Collections.Generic;

namespace Demo
{
    /// <summary>Демонстрация подсветки синтаксиса.</summary>
    public sealed class Greeter
    {
        private readonly string _name;
        public Greeter(string name) => _name = name;

        public IEnumerable<int> Fibonacci(int n)
        {
            int a = 0, b = 1;
            for (var i = 0; i < n; i++)
            {
                yield return a;
                (a, b) = (b, a + b);   // деконструкция кортежа
            }
        }

        public void Run() => Console.WriteLine($""Привет, {_name}! 3.14 == {Math.PI:F2}"");
    }
}

/* Проверь:  выделение мышью · Ctrl+F (поиск) · номера строк · скролл ·
   открой реальный файл командой:  SeriousView C:\path\to\file.rs  */
";
}
