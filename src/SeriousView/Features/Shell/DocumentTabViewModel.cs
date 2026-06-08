using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Documents;
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

    /// <summary>Caret line/column (1-based), pushed from the editor; surfaced in the status bar
    /// while the source editor is visible.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretText))]
    private int _caretLine = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaretText))]
    private int _caretColumn = 1;

    public string CaretText => $"Стр {CaretLine}, Кол {CaretColumn}";

    /// <summary>Go-to-line overlay (Ctrl+G) state, shown over the source editor.</summary>
    [ObservableProperty]
    private bool _isGoToLineOpen;

    [ObservableProperty]
    private string _goToLineText = "";

    /// <summary>Raised with a clamped 1-based line when the user submits; the view scrolls there.</summary>
    public event Action<int>? GoToLineRequested;

    [RelayCommand]
    private void SubmitGoToLine()
    {
        if (int.TryParse(GoToLineText, out var line))
        {
            var max = Math.Max(1, TextMetrics.LineCount(DocumentText));
            GoToLineRequested?.Invoke(Math.Clamp(line, 1, max));
        }

        CloseGoToLine();
    }

    [RelayCommand]
    private void CloseGoToLine()
    {
        IsGoToLineOpen = false;
        GoToLineText = "";
    }

    /// <summary>Document text, bound one-way into the editor. Named DocumentText (not
    /// Content) to avoid colliding with TabViewItem.Content when bound inside a TabView.</summary>
    public string DocumentText { get; }

    /// <summary>Shared editor display options (font/wrap/line-numbers), assigned by the shell when
    /// the tab is added. The source editor binds to it; null in unit fixtures.</summary>
    public EditorOptions? Editor { get; set; }

    /// <summary>Shared shell-layout options (reading mode), assigned by the shell when the tab is
    /// added — same pattern as <see cref="Editor"/>. The preview binds to it; null in unit fixtures.</summary>
    public LayoutOptions? Layout { get; set; }

    /// <summary>True for markdown files — drives whether a rendered preview is offered.</summary>
    public bool IsMarkdown => MarkdownFile.IsMarkdownExtension(GrammarExtension);

    /// <summary>Preview vs source. Defaults to Preview; only meaningful for markdown
    /// (for code files <see cref="ShowSource"/> short-circuits to true regardless).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ViewModeLabel))]
    private DocumentViewMode _viewMode = DocumentViewMode.Preview;

    /// <summary>Show the rendered markdown preview (markdown file in Preview mode, has content).</summary>
    public bool ShowPreview => !ShowNotice && IsMarkdown && ViewMode == DocumentViewMode.Preview;

    /// <summary>Show the source editor (any non-markdown file, or markdown in Source mode).</summary>
    public bool ShowSource => !ShowNotice && (!IsMarkdown || ViewMode == DocumentViewMode.Source);

    /// <summary>Content classification from the loader (Text / Binary / TooLarge).</summary>
    public FileLoadKind Kind { get; }

    /// <summary>Detected text encoding (e.g. "UTF-8", "Windows-1251"); "" for non-text.</summary>
    public string EncodingName { get; }

    /// <summary>Dominant line ending ("LF"/"CRLF"/"CR"/"Mixed"); "" when none / non-text.</summary>
    public string LineEnding { get; }

    /// <summary>True when the file is too big to syntax-highlight (shown as plain text).</summary>
    public bool HighlightSuppressed { get; }

    private readonly long _sizeBytes;

    /// <summary>A binary / too-large / empty document shows a notice instead of editor + preview.</summary>
    public bool ShowNotice => Kind != FileLoadKind.Text || DocumentText.Length == 0;

    /// <summary>Message for the notice overlay (only meaningful when <see cref="ShowNotice"/>).</summary>
    public string NoticeText => Kind switch
    {
        FileLoadKind.Binary => "Бинарный файл — просмотр недоступен",
        FileLoadKind.TooLarge => $"Файл слишком большой для просмотра ({SizeText})",
        _ => "Файл пуст",
    };

    private string SizeText => _sizeBytes switch
    {
        >= 1024 * 1024 => $"{_sizeBytes / (1024.0 * 1024):0.#} МБ",
        >= 1024 => $"{_sizeBytes / 1024.0:0.#} КБ",
        _ => $"{_sizeBytes} Б",
    };

    /// <summary>Base directory for resolving relative image/asset paths in the preview.</summary>
    public string? AssetPathRoot => FilePath is null ? null : Path.GetDirectoryName(FilePath);

    private string? _previewMarkdown;

    /// <summary>Markdown handed to the renderer — empty for non-markdown files so the
    /// (hidden) preview never parses code as markdown. Run through the Core preprocessor
    /// (admonitions, task lists, footnotes). Cached: the document text is immutable.</summary>
    public string PreviewMarkdown =>
        _previewMarkdown ??= IsMarkdown ? MarkdownPreprocessor.Transform(DocumentText) : "";

    /// <summary>Label for the preview/source toggle, reflecting the current mode.</summary>
    public string ViewModeLabel => ViewMode == DocumentViewMode.Preview ? "Предпросмотр" : "Исходник";

    /// <summary>Flip preview ⟷ source. Only enabled for markdown documents.</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ToggleViewMode()
        => ViewMode = ViewMode == DocumentViewMode.Preview ? DocumentViewMode.Source : DocumentViewMode.Preview;

    private IReadOnlyList<HeadingOutline>? _outline;

    /// <summary>Heading outline (table of contents). Empty for non-markdown files.
    /// Cached: the document text is immutable.</summary>
    public IReadOnlyList<HeadingOutline> Outline =>
        IsMarkdown ? _outline ??= MarkdownOutline.Parse(DocumentText) : [];

    /// <summary>True when the document has at least one heading (drives the outline pane).</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>Raised when the user picks a heading; the view scrolls preview/source to it.</summary>
    public event Action<HeadingOutline>? NavigationRequested;

    /// <summary>Ask the view to navigate to <paramref name="heading"/> (bound from the outline).</summary>
    [RelayCommand]
    private void NavigateToHeading(HeadingOutline? heading)
    {
        if (heading is not null)
            NavigationRequested?.Invoke(heading);
    }

    private DocumentTabViewModel(string header, FileLoadResult load)
    {
        _header = header;
        DocumentText = load.Text;
        Kind = load.Kind;
        EncodingName = load.EncodingName;
        LineEnding = load.LineEnding;
        HighlightSuppressed = load.HighlightSuppressed;
        _sizeBytes = load.SizeBytes;
    }

    /// <summary>Build a tab from a loaded file (the main entry point).</summary>
    public static DocumentTabViewModel FromLoad(FileLoadResult load, string path)
    {
        var tab = new DocumentTabViewModel(Path.GetFileName(path), load)
        {
            FilePath = path,
            GrammarExtension = Path.GetExtension(path),
        };
        tab.StatusText = tab.BuildStatus();
        return tab;
    }

    /// <summary>Convenience for in-memory text (tests, fixtures): a UTF-8 text document.</summary>
    public static DocumentTabViewModel FromFile(string text, string path)
        => FromLoad(FileLoadResult.ForText(text, "UTF-8", LineEndings.Detect(text), text.Length), path);

    public static DocumentTabViewModel CreateSample()
    {
        var tab = new DocumentTabViewModel(
            "Пример", FileLoadResult.ForText(Sample, "UTF-8", LineEndings.Detect(Sample), Sample.Length))
        {
            GrammarExtension = ".cs",
        };
        tab.StatusText = $"Строк: {TextMetrics.LineCount(Sample)}   ·   подсветка: C#";
        return tab;
    }

    private string BuildStatus() => Kind switch
    {
        FileLoadKind.Binary => $"Бинарный файл · {SizeText}",
        FileLoadKind.TooLarge => $"Слишком большой · {SizeText}",
        _ when DocumentText.Length == 0 => "Пустой файл",
        _ => $"Строк: {TextMetrics.LineCount(DocumentText)}   ·   Символов: {TextMetrics.CharCount(DocumentText)}"
           + $"   ·   {EncodingName}"
           + (LineEnding.Length > 0 ? $"   ·   {LineEnding}" : "")
           + (HighlightSuppressed ? "   ·   без подсветки" : ""),
    };

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
