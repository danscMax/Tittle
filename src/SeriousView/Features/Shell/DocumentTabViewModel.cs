using System;
using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SeriousView.Core.Documents;
using SeriousView.Core.Services;
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

    /// <summary>True for the one tab whose body is shown. The shell keeps every open tab's DocumentView
    /// realized and toggles its visibility on this flag, so switching tabs doesn't rebuild the editor /
    /// TextMate / markdown render.</summary>
    [ObservableProperty]
    private bool _isActive;

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

    // --- In-document find (Ctrl+F). Source only; matches are computed over DocumentText and the editor
    //     highlights them. Per-tab, mirroring go-to-line. Replace is deferred to M15 (editing + save). ---

    /// <summary>Find-bar visibility (Ctrl+F opens, Esc closes).</summary>
    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _searchCaseSensitive;

    [ObservableProperty]
    private bool _searchRegex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchStatus))]
    private int _searchMatchCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchStatus))]
    private int _searchCurrentIndex = -1; // -1 = no current match

    /// <summary>Regex mode is on but the pattern doesn't compile — the regex toggle goes red.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchStatus))]
    private bool _searchInvalidRegex;

    /// <summary>Find-bar counter: "3 / 12", or a short status when there's nothing to count.</summary>
    public string SearchStatus =>
        SearchMatchCount > 0 ? $"{SearchCurrentIndex + 1} / {SearchMatchCount}"
        : SearchQuery.Length == 0 ? ""
        : SearchInvalidRegex ? "ошибка"
        : "нет совпадений";

    private IReadOnlyList<MatchRange> _searchMatches = Array.Empty<MatchRange>();

    /// <summary>Current match ranges, read by the view's highlight renderer.</summary>
    public IReadOnlyList<MatchRange> SearchMatches => _searchMatches;

    /// <summary>Matches / active index changed — the view re-highlights and (if a match is current)
    /// scrolls the editor to it. Mirrors <see cref="GoToLineRequested"/>.</summary>
    public event Action? SearchUpdated;

    partial void OnSearchQueryChanged(string value) => RecomputeSearch();
    partial void OnSearchCaseSensitiveChanged(bool value) => RecomputeSearch();
    partial void OnSearchRegexChanged(bool value) => RecomputeSearch();

    // Re-run the search and jump to the first match — incremental, as the user types or flips a toggle.
    private void RecomputeSearch()
    {
        var outcome = TextSearch.FindAll(DocumentText, SearchQuery, SearchCaseSensitive, SearchRegex);
        _searchMatches = outcome.Matches;
        SearchInvalidRegex = SearchRegex && !outcome.PatternValid;
        SearchMatchCount = _searchMatches.Count;
        SearchCurrentIndex = _searchMatches.Count > 0 ? 0 : -1;
        SearchUpdated?.Invoke();
    }

    /// <summary>Open the find bar. For a markdown tab in Preview, switch to Source first (find operates
    /// over the source text, so matches must be visible there); a notice tab has nothing to search.</summary>
    [RelayCommand]
    private void OpenSearch()
    {
        if (ShowNotice)
            return;
        if (IsMarkdown && ViewMode == DocumentViewMode.Preview)
            ViewMode = DocumentViewMode.Source;
        IsSearchOpen = true;
        RecomputeSearch();
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchOpen = false;
        _searchMatches = Array.Empty<MatchRange>();
        SearchMatchCount = 0;
        SearchCurrentIndex = -1;
        SearchInvalidRegex = false;
        SearchUpdated?.Invoke(); // clear the highlights
    }

    [RelayCommand]
    private void NextMatch() => StepSearch(forward: true);

    [RelayCommand]
    private void PreviousMatch() => StepSearch(forward: false);

    // Move relative to the current match (its offset is the anchor), wrapping at the ends.
    private void StepSearch(bool forward)
    {
        if (_searchMatches.Count == 0)
            return;

        var anchor = SearchCurrentIndex >= 0 ? _searchMatches[SearchCurrentIndex] : new MatchRange(-1, 0);
        SearchCurrentIndex = forward
            ? TextSearch.NextMatchIndex(_searchMatches, anchor.Offset)
            : TextSearch.PreviousMatchIndex(_searchMatches, anchor.Offset + anchor.Length);
        SearchUpdated?.Invoke();
    }

    [RelayCommand]
    private void ToggleSearchCaseSensitive() => SearchCaseSensitive = !SearchCaseSensitive;

    [RelayCommand]
    private void ToggleSearchRegex() => SearchRegex = !SearchRegex;

    /// <summary>Document text, bound one-way into the editor. Named DocumentText (not
    /// Content) to avoid colliding with TabViewItem.Content when bound inside a TabView.</summary>
    public string DocumentText { get; }

    /// <summary>Shared editor display options (font/wrap/line-numbers), assigned by the shell when
    /// the tab is added. The source editor binds to it; null in unit fixtures.</summary>
    public EditorOptions? Editor { get; set; }

    /// <summary>Shared shell-layout options (reading mode), assigned by the shell when the tab is
    /// added — same pattern as <see cref="Editor"/>. The preview binds to it; null in unit fixtures.</summary>
    public LayoutOptions? Layout { get; set; }

    /// <summary>Back-reference to the owning shell, assigned when the tab is added (same pattern as
    /// <see cref="Editor"/> / <see cref="Layout"/>). The tab's context menu binds the shell's tab
    /// commands (close-others / close-to-right / close-all, and copy / reveal) through it — a context
    /// flyout opens in a popup, so its bindings can't walk the visual tree up to the shell. Null in
    /// unit fixtures that don't add the tab through the shell.</summary>
    public MainWindowViewModel? Shell { get; set; }

    /// <summary>True for markdown files — drives whether a rendered preview is offered.</summary>
    public bool IsMarkdown => MarkdownFile.IsMarkdownExtension(GrammarExtension);

    /// <summary>True for .json files — offers the display-only pretty-print toggle (ported).</summary>
    public bool IsJson => string.Equals(GrammarExtension, ".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Delimiter for tabular files, or null (drives the csv-as-table view; ported).</summary>
    public char? Delimiter => GrammarExtension?.ToLowerInvariant() switch
    {
        ".csv" => ',',
        ".tsv" => '\t',
        _ => null,
    };

    private CsvTableViewModel? _csvTable;
    private bool _csvTableBuilt;

    /// <summary>Sortable table model for .csv/.tsv tabs; null when the file doesn't parse
    /// (the source view shows instead). Cached: the document text is immutable.</summary>
    public CsvTableViewModel? CsvTable
    {
        get
        {
            if (!_csvTableBuilt)
            {
                _csvTableBuilt = true;
                if (Delimiter is { } delimiter
                    && DelimitedTable.Parse(DocumentText, delimiter) is { } table)
                    _csvTable = new CsvTableViewModel(table);
            }

            return _csvTable;
        }
    }

    /// <summary>Per-tab table-vs-source choice; new tabs inherit the persisted default.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCsvTable))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ShowMinimap))]
    private bool _csvAsTableEnabled;

    /// <summary>Show the table view (delimited file that parsed, table mode on).</summary>
    public bool ShowCsvTable => !ShowNotice && CsvAsTableEnabled && CsvTable is not null;

    /// <summary>Flip table ⟷ source for delimited tabs (and remember it as the default).</summary>
    [RelayCommand]
    private void ToggleCsvView()
    {
        CsvAsTableEnabled = !CsvAsTableEnabled;
        if (Editor is not null)
            Editor.CsvAsTable = CsvAsTableEnabled;
    }

    /// <summary>Per-tab pretty-print state; new tabs inherit the persisted default from
    /// <see cref="Shared.EditorOptions.JsonPretty"/> when the shell adopts them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText))]
    private bool _jsonPrettyEnabled;

    private string? _prettyJson;
    private string? _smartText;

    /// <summary>True for prose-text files (.txt/.log) — they get the text outline and the
    /// display-only smart typography (ported).</summary>
    public bool IsPlainText => GrammarExtension is ".txt" or ".log";

    /// <summary>Per-tab smart-typography state; inherits the persisted default on adoption.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText))]
    private bool _smartTypographyEnabled;

    /// <summary>Toggle smart typography for this tab (and remember it as the default).</summary>
    [RelayCommand]
    private void ToggleSmartTypography()
    {
        SmartTypographyEnabled = !SmartTypographyEnabled;
        if (Editor is not null)
            Editor.SmartTypography = SmartTypographyEnabled;
    }

    /// <summary>What the source editor shows: the document text, or a display-only transform —
    /// pretty-printed JSON / smart typography for prose text. The raw text stays the single
    /// source of truth: exports, reload and search see the file as written.</summary>
    public string SourceText =>
        IsJson && JsonPrettyEnabled
            ? _prettyJson ??= JsonPrettyPrinter.TryFormat(DocumentText) ?? DocumentText
        : IsPlainText && SmartTypographyEnabled
            ? _smartText ??= SmartTypography.Apply(DocumentText)
        : DocumentText;

    /// <summary>Toggle pretty-print for this tab (and remember it as the default).</summary>
    [RelayCommand]
    private void ToggleJsonPretty()
    {
        JsonPrettyEnabled = !JsonPrettyEnabled;
        if (Editor is not null)
            Editor.JsonPretty = JsonPrettyEnabled;
    }

    /// <summary>Preview vs source. Defaults to Preview; only meaningful for markdown
    /// (for code files <see cref="ShowSource"/> short-circuits to true regardless).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ShowMinimap))]
    [NotifyPropertyChangedFor(nameof(ViewModeToggleTip))]
    private DocumentViewMode _viewMode = DocumentViewMode.Preview;

    /// <summary>Show the rendered markdown preview (markdown file in Preview mode, has content).</summary>
    public bool ShowPreview => !ShowNotice && IsMarkdown && ViewMode == DocumentViewMode.Preview;

    /// <summary>Show the source editor (any non-markdown file, or markdown in Source mode),
    /// unless the tab is showing its table view.</summary>
    public bool ShowSource =>
        !ShowNotice && (!IsMarkdown || ViewMode == DocumentViewMode.Source) && !ShowCsvTable;

    /// <summary>Show the code minimap beside the source editor (ported): code/text tabs with
    /// a symbol outline; markdown keeps the TOC sidebar instead.</summary>
    public bool ShowMinimap => ShowSource && !IsMarkdown && HasOutline;

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
    /// (wiki links, admonitions, task lists, footnotes). Cached: the document text is
    /// immutable, so wiki-link existence snapshots once per tab (the click-time command
    /// re-checks; M14 live-reload will refresh naturally).</summary>
    public string PreviewMarkdown =>
        _previewMarkdown ??= IsMarkdown
            ? MarkdownPreprocessor.Transform(DocumentText, BuildWikiResolver())
            : "";

    /// <summary>Wiki-name resolver for the preprocessor and the HTML exporter: a sibling
    /// <c>&lt;name&gt;.md</c> next to this document. Null for path-less tabs (sample) — nothing
    /// resolves. File I/O lives here in the UI layer; Core only sees the delegate.</summary>
    internal Func<string, bool>? BuildWikiResolver()
        => AssetPathRoot is not { } root
            ? null
            : name => File.Exists(Path.Combine(root, name + ".md"));

    /// <summary>Tooltip for the status-bar view toggle — names the switch target (the action).</summary>
    public string ViewModeToggleTip =>
        ViewMode == DocumentViewMode.Preview ? "Показать исходник" : "Показать предпросмотр";

    /// <summary>Flip preview ⟷ source. Only enabled for markdown documents.</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ToggleViewMode()
        => ViewMode = ViewMode == DocumentViewMode.Preview ? DocumentViewMode.Source : DocumentViewMode.Preview;

    private IReadOnlyList<HeadingOutline>? _outline;

    /// <summary>Document outline (table of contents): markdown headings, code symbols
    /// (per-language heuristics) or plain-text headings — all the same shape, so the TOC
    /// panel, breadcrumbs and scroll-spy work for every file type (ported). Cached: the
    /// document text is immutable.</summary>
    public IReadOnlyList<HeadingOutline> Outline => _outline ??=
        IsMarkdown ? MarkdownOutline.Parse(DocumentText)
        : SymbolOutline.Supports(GrammarExtension) ? SymbolOutline.Parse(DocumentText, GrammarExtension)
        : GrammarExtension is ".txt" or ".log" ? TextOutline.Parse(DocumentText)
        : [];

    /// <summary>True when the document has at least one heading (drives the outline pane).</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>True when the file changed (or vanished) on disk since this tab loaded it —
    /// shown as the tab's dirty dot (M14). Reloading clears it by replacing the tab.</summary>
    [ObservableProperty]
    private bool _isChangedOnDisk;

    /// <summary>Status-bar text for the current editor selection («выдел.: N слов»), written
    /// by <c>DocumentView</c> from the editor's selection (ported selection word count).
    /// Empty when nothing is selected.</summary>
    [ObservableProperty]
    private string _selectionInfo = string.Empty;

    /// <summary>Status-bar "NN%" through the document, written by <c>DocumentView</c> per
    /// scroll event in either view mode (ported). Empty when the document fits the viewport.</summary>
    [ObservableProperty]
    private string _scrollPercentText = string.Empty;

    /// <summary>Current reading position, written by <c>DocumentView</c> per scroll event
    /// (the <see cref="CaretLine"/> pattern) — the shell hands it to a reload's fresh tab.</summary>
    public HeadingAnchor ReadingAnchor { get; set; } = new(-1, 0);

    /// <summary>One-shot: set by the shell on a reload's replacement tab; consumed by the new
    /// view after its first layout to land on the same document position (M14).</summary>
    public HeadingAnchor? RestoreAnchor { get; set; }

    /// <summary>Ordinal of the heading currently at the top of the view, −1 above the first —
    /// written by <c>DocumentView</c> from the scroll position (like <see cref="CaretLine"/>).
    /// Drives the outline's active marker and the breadcrumbs (M10).</summary>
    [ObservableProperty]
    private int _activeHeadingOrdinal = -1;

    /// <summary>Ancestor chain of the active heading (top-level first), shown as the
    /// breadcrumbs strip. Empty above the first heading and for non-markdown.</summary>
    public IReadOnlyList<HeadingOutline> Breadcrumbs =>
        MarkdownOutline.AncestorChain(Outline, ActiveHeadingOrdinal);

    partial void OnActiveHeadingOrdinalChanged(int value)
    {
        OnPropertyChanged(nameof(Breadcrumbs));
        // Reading a heading marks it visited (ported md-visited-*): the TOC unread dot fades.
        if (FilePath is not null && value >= 0 && ViewState is { } store)
        {
            store.MarkVisited(FilePath, value);
            ViewStateVersion++;
        }
    }

    /// <summary>Per-document visited/bookmark state (ported), shared via the shell; null in
    /// tests or for the sample tab — everything degrades to "no marks".</summary>
    public ViewStateStore? ViewState { get; set; }

    // ---- in-place editing (M15): DocumentText stays the loaded truth; the live editor
    //      buffer is reached through a pull seam and persisted by the shell's Save. ----

    /// <summary>True while the editor buffer differs from the loaded document (drives the
    /// unsaved-changes marker on the tab). Written by <c>DocumentView</c> on text changes;
    /// cleared by Save (the follow-up watcher reload swaps in a clean tab anyway).</summary>
    [ObservableProperty]
    private bool _isEdited;

    /// <summary>Pull seam to the live editor text, wired by <c>DocumentView</c>; null when
    /// no view is attached (then there is nothing unsaved to pull).</summary>
    public Func<string>? EditorTextProvider { get; set; }

    /// <summary>Bumped on every visited/bookmark mutation so the TOC multi-bindings recompute.</summary>
    [ObservableProperty]
    private int _viewStateVersion;

    public bool IsHeadingVisited(int ordinal)
        => FilePath is null || ViewState is null || ViewState.IsVisited(FilePath, ordinal);

    public bool IsHeadingBookmarked(int ordinal)
        => FilePath is not null && ViewState is not null && ViewState.IsBookmarked(FilePath, ordinal);

    /// <summary>Bookmark glyph toggle on a TOC item; flushes immediately (a rare, explicit act).</summary>
    [RelayCommand]
    private void ToggleBookmark(HeadingOutline? heading)
    {
        if (heading is null || FilePath is null || ViewState is null)
            return;
        ViewState.ToggleBookmark(FilePath, heading.Ordinal);
        ViewState.Flush();
        ViewStateVersion++;
    }

    /// <summary>Raised when the user picks a heading; the view scrolls preview/source to it.</summary>
    public event Action<HeadingOutline>? NavigationRequested;

    /// <summary>Ask the view to navigate to <paramref name="heading"/> (bound from the outline).</summary>
    [RelayCommand]
    private void NavigateToHeading(HeadingOutline? heading)
    {
        if (heading is not null)
            NavigationRequested?.Invoke(heading);
    }

    /// <summary>Raised by the fold-all/unfold-all commands (ported section folding);
    /// the view owns the folding state, the same seam shape as navigation.</summary>
    public event Action<bool>? FoldAllRequested;

    [RelayCommand]
    private void FoldAllSections() => FoldAllRequested?.Invoke(true);

    [RelayCommand]
    private void UnfoldAllSections() => FoldAllRequested?.Invoke(false);

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
