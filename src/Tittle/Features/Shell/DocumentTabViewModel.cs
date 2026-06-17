using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tittle.Core.Abstractions;
using Tittle.Core.Documents;
using Tittle.Core.Editing;
using Tittle.Core.Services;
using Tittle.Core.Text;
using Tittle.Features.Viewer.Pdf;
using Tittle.Shared;

namespace Tittle.Features.Shell;

/// <summary>How a markdown document is shown: rendered preview or raw source.
/// Non-markdown files are always source (the toggle is hidden).</summary>
public enum DocumentViewMode
{
    Preview,
    Source,

    /// <summary>Source editor and rendered preview shown side by side (markdown only),
    /// with live mutual scroll sync. See <see cref="DocumentTabViewModel.ShowSplit"/>.</summary>
    Split,
}

/// <summary>One open document = one tab. Holds its content, grammar, header and
/// per-document status metrics. Content is pushed to the editor via
/// <c>EditorBehavior.Text</c> (AvaloniaEdit can't bind Document directly).</summary>
public partial class DocumentTabViewModel : ViewModelBase, IDisposable
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

    /// <summary>Raised with a clamped 1-based page when the user submits on a PDF tab (Ctrl+G reuses
    /// the go-to-line input); the PDF view scrolls to that page.</summary>
    public event Action<int>? PdfGoToPageRequested;

    [RelayCommand]
    private void SubmitGoToLine()
    {
        if (int.TryParse(GoToLineText, out var target))
        {
            if (IsPdf)
                PdfGoToPageRequested?.Invoke(Math.Clamp(target, 1, Math.Max(1, Pdf?.PageCount ?? 1)));
            else
                GoToLineRequested?.Invoke(Math.Clamp(target, 1, Math.Max(1, TextMetrics.LineCount(DocumentText))));
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

    // Typing debounces on large documents (a burst → one scan); a single toggle never bursts, so
    // those re-scan immediately.
    partial void OnSearchQueryChanged(string value) => ScheduleSearch();
    partial void OnSearchCaseSensitiveChanged(bool value) => RecomputeSearch();
    partial void OnSearchRegexChanged(bool value) => RecomputeSearch();

    // Large documents re-scan the whole buffer on every keystroke; coalesce a typing burst into one
    // scan. Small documents (the common case + test fixtures) stay synchronous — the scan is instant
    // and the "set query → results" contract holds. The timer is created lazily on the first
    // large-doc search (so plain unit fixtures never construct a DispatcherTimer).
    private const int SearchDebounceThreshold = 200_000;
    private DispatcherTimer? _searchDebounceTimer;

    internal bool SearchDebouncePending => _searchDebounceTimer is { IsEnabled: true };

    private void ScheduleSearch()
    {
        if (DocumentText.Length <= SearchDebounceThreshold)
        {
            RecomputeSearch();
            return;
        }

        if (_searchDebounceTimer is null)
        {
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _searchDebounceTimer.Tick += OnSearchDebounceTick;
        }

        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void OnSearchDebounceTick(object? sender, EventArgs e)
    {
        _searchDebounceTimer?.Stop();
        RecomputeSearch();
    }

    /// <summary>Stop and detach the large-doc search debounce timer when the tab closes, so a
    /// pending tick can't keep the closed tab VM (and its whole document string) rooted on the
    /// dispatcher timer queue. Called by the shell from every tab-close / replace path.</summary>
    public void Dispose()
    {
        if (_searchDebounceTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnSearchDebounceTick;
            _searchDebounceTimer = null;
        }

        // Detach from the shared diagram options so a closed tab isn't rooted by the singleton.
        if (_diagrams is not null)
            _diagrams.PropertyChanged -= OnDiagramsChanged;
    }

    // Run a pending (debounced) scan now — so next/prev act on fresh matches after a fast type+Enter.
    private void FlushPendingSearch()
    {
        if (_searchDebounceTimer is { IsEnabled: true })
        {
            _searchDebounceTimer.Stop();
            RecomputeSearch();
        }
    }

    // Re-run the search and jump to the first match — incremental, as the user types or flips a toggle.
    private void RecomputeSearch()
    {
        // Scan the LIVE editor text when an editor is attached (so find reflects unsaved edits and
        // post-replace content); fall back to the loaded DocumentText in headless unit fixtures.
        var text = EditorActions?.Text ?? DocumentText;
        var outcome = TextSearch.FindAll(text, SearchQuery, SearchCaseSensitive, SearchRegex);
        _searchMatches = outcome.Matches;
        SearchInvalidRegex = SearchRegex && !outcome.PatternValid;
        SearchMatchCount = _searchMatches.Count;
        SearchCurrentIndex = _searchMatches.Count > 0 ? 0 : -1;
        SearchUpdated?.Invoke();
    }

    /// <summary>Open the find bar. For a markdown tab in Preview, switch to Source first (find operates
    /// over the source text, so matches must be visible there); a notice tab has nothing to search.</summary>
    [RelayCommand]
    private void OpenSearch() => OpenFindBar(replaceMode: false);

    /// <summary>Open the find bar with the replace row (Ctrl+H).</summary>
    [RelayCommand]
    private void OpenReplace() => OpenFindBar(replaceMode: true);

    private void OpenFindBar(bool replaceMode)
    {
        if (ShowNotice)
            return;
        if (IsMarkdown && ViewMode == DocumentViewMode.Preview)
            ViewMode = DocumentViewMode.Source;
        IsReplaceMode = replaceMode;
        IsSearchOpen = true;
        RecomputeSearch();
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchOpen = false;
        _searchDebounceTimer?.Stop(); // cancel any pending large-doc scan
        _searchMatches = Array.Empty<MatchRange>();
        SearchMatchCount = 0;
        SearchCurrentIndex = -1;
        SearchInvalidRegex = false;
        SearchUpdated?.Invoke(); // clear the highlights
    }

    [RelayCommand]
    private void NextMatch()
    {
        // Record a FindNext intent while a macro is recording, so find-driven edits replay (the intent
        // is non-wrapping on replay → it ends an until-EOF run at the last match).
        if (Shell?.IsRecordingMacro == true && SearchQuery.Length > 0)
            Shell.RecordIntent(new FindNextIntent(SearchQuery, SearchRegex, SearchCaseSensitive));
        StepSearch(forward: true);
    }

    [RelayCommand]
    private void PreviousMatch() => StepSearch(forward: false);

    // Move relative to the current match (its offset is the anchor), wrapping at the ends.
    private void StepSearch(bool forward)
    {
        FlushPendingSearch(); // a fast type+Enter on a large doc must step over fresh matches
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

    // --- Replace (Ctrl+H). Operates on the LIVE editor text via EditorActions; matches re-scan after. ---

    /// <summary>Whether the find bar shows the replace row (Ctrl+H opens it; Ctrl+F is find-only).</summary>
    [ObservableProperty]
    private bool _isReplaceMode;

    [ObservableProperty]
    private string _replaceText = "";

    /// <summary>Replace the current match (regex group-substitution honoured), then advance to the next.
    /// With no current match selected yet, it just steps to the first one.</summary>
    [RelayCommand]
    private void ReplaceCurrent()
    {
        if (EditorActions is not { } actions || SearchQuery.Length == 0)
            return;

        FlushPendingSearch();
        if (_searchMatches.Count == 0 || SearchCurrentIndex < 0 || SearchCurrentIndex >= _searchMatches.Count)
        {
            StepSearch(forward: true); // select the first/next match; the following press replaces it
            return;
        }

        var match = _searchMatches[SearchCurrentIndex];
        var matchedText = actions.Text.Substring(match.Offset, match.Length);
        var replacement = SearchRegex
            ? TextSearch.ReplaceAll(matchedText, SearchQuery, ReplaceText, SearchCaseSensitive, regex: true).NewText
            : ReplaceText;

        // Record a ReplaceSelection intent while recording. For a regex replace, carry the pattern + flags
        // so replay re-runs the group substitution ($1, $2 …) against each match instead of inserting the
        // literal template; a literal replace records just the text (Pattern stays null → unchanged behavior).
        if (Shell?.IsRecordingMacro == true)
            Shell.RecordIntent(SearchRegex
                ? new ReplaceSelectionIntent(ReplaceText, SearchQuery, Regex: true, SearchCaseSensitive)
                : new ReplaceSelectionIntent(ReplaceText));
        actions.Replace(match.Offset, match.Length, replacement);
        RecomputeSearch();          // the edit shifts offsets → re-scan the live text
        StepSearch(forward: true);  // advance to the next match
    }

    /// <summary>Replace every match in the document in one undo step, then re-scan.</summary>
    [RelayCommand]
    private void ReplaceAll()
    {
        if (EditorActions is not { } actions || SearchQuery.Length == 0)
            return;

        var live = actions.Text;
        var result = TextSearch.ReplaceAll(live, SearchQuery, ReplaceText, SearchCaseSensitive, SearchRegex);
        SearchInvalidRegex = SearchRegex && !result.PatternValid;
        if (result.Count == 0)
            return;

        actions.Replace(0, live.Length, result.NewText);
        RecomputeSearch();
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

    private DiagramOptions? _diagrams;

    /// <summary>Shared diagram (Kroki) options (M12), assigned by the shell — same pattern as
    /// <see cref="Editor"/>. Drives whether the preprocessor turns diagram fences into rendered
    /// diagrams; toggling it live-invalidates this tab's preview markdown.</summary>
    public DiagramOptions? Diagrams
    {
        get => _diagrams;
        set
        {
            if (_diagrams is not null)
                _diagrams.PropertyChanged -= OnDiagramsChanged;
            _diagrams = value;
            if (_diagrams is not null)
            {
                _diagrams.PropertyChanged += OnDiagramsChanged;
                // FromLoad warms PreviewMarkdown with diagrams OFF (Diagrams isn't assigned yet);
                // if they're actually on, that warm is stale — drop it so the next read recomputes.
                if (_diagrams.Enabled && _previewMarkdown is not null)
                {
                    _previewMarkdown = null;
                    OnPropertyChanged(nameof(PreviewMarkdown));
                }
            }
        }
    }

    private void OnDiagramsChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only Enabled changes the markdown string (fences ↔ ::: diagram containers) → drop the
        // cache and re-emit so the preview rebuilds live. A URL change leaves the string identical
        // (the handler reads the URL at render time), so it applies on the next reload/reopen.
        if (e.PropertyName == nameof(DiagramOptions.Enabled))
        {
            _previewMarkdown = null;
            OnPropertyChanged(nameof(PreviewMarkdown));
        }
    }

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

    /// <summary>True for XML-family files — offers the display-only pretty-print toggle.</summary>
    public bool IsXml => GrammarExtension?.ToLowerInvariant()
        is ".xml" or ".csproj" or ".props" or ".targets" or ".config" or ".xaml" or ".axaml";

    /// <summary>True for newline-delimited JSON (.ndjson/.jsonl) — pretty-printed per line.</summary>
    public bool IsNdjson => GrammarExtension?.ToLowerInvariant() is ".ndjson" or ".jsonl";

    /// <summary>True when the source view offers the display-only «формат» (pretty-print) toggle —
    /// JSON, XML or NDJSON. One toggle, dispatched by type in <see cref="SourceText"/>.</summary>
    public bool IsPrettyPrintable => IsJson || IsXml || IsNdjson;

    /// <summary>Delimiter for tabular files, or null (drives the csv-as-table view; ported).</summary>
    public char? Delimiter => GrammarExtension?.ToLowerInvariant() switch
    {
        ".csv" => ',',
        ".tsv" => '\t',
        _ => null,
    };

    /// <summary>True for key/value config files (.toml/.ini/.env/.editorconfig) — rendered in the
    /// shared table overlay as a Ключ/Значение «Метаданные» view (the standalone-file counterpart
    /// of the markdown front-matter panel).</summary>
    public bool IsKeyValueConfig =>
        GrammarExtension?.ToLowerInvariant() is ".toml" or ".ini" or ".env" or ".editorconfig";

    // --- PDF (rendered page-by-page by PdfView; the heavy native render is off the UI thread) ---

    /// <summary>True for a PDF document — shown by the dedicated PdfView, not as text.</summary>
    public bool IsPdf => Kind == FileLoadKind.Pdf;

    private PdfPagesViewModel? _pdf;
    private bool _pdfBuilt;

    /// <summary>Lazily-opened PDF page model; null when PDFium can't render it on this platform or
    /// file (PdfView then shows the "open externally" fallback). Built on first bind, NOT in
    /// FromLoad — it touches the native engine + disk.</summary>
    public PdfPagesViewModel? Pdf
    {
        get
        {
            if (!_pdfBuilt)
            {
                _pdfBuilt = true;
                if (IsPdf && FilePath is { } path)
                    _pdf = PdfPagesViewModel.TryOpen(path);
            }

            return _pdf;
        }
    }

    /// <summary>Show the PDF view (the source/preview/table hosts stay hidden for a PDF tab).</summary>
    public bool ShowPdf => Kind == FileLoadKind.Pdf;

    /// <summary>«стр N / M» for the status bar, written by <c>PdfView</c> from the scroll position.</summary>
    [ObservableProperty]
    private string _pdfPageText = "";

    /// <summary>PDF page sizing: false = fit-to-width (default), true = actual size (100%).</summary>
    [ObservableProperty]
    private bool _pdfActualSize;

    /// <summary>Toggle PDF fit-to-width ⟷ actual size (100%).</summary>
    [RelayCommand]
    private void TogglePdfFit() => PdfActualSize = !PdfActualSize;

    /// <summary>Open this document in the OS default application (PDF / image fallback / convenience).</summary>
    [RelayCommand]
    private void OpenExternally()
    {
        if (FilePath is { } path)
            Shell?.OpenExternally(path);
    }

    // --- Image files (raster decoded by Avalonia/Skia; .svg via SvgImage — both are IImage) ---

    /// <summary>True for an image file — shown by the dedicated ImageFileView, not as text.</summary>
    public bool IsImage => Kind == FileLoadKind.Image;

    /// <summary>True for a vector .svg image (a subset of <see cref="IsImage"/>).</summary>
    public bool IsSvg => ImageFile.IsSvgExtension(GrammarExtension);

    private IImage? _imageSource;
    private bool _imageBuilt;

    /// <summary>Lazily-loaded image (raster <see cref="Bitmap"/> or vector <see cref="SvgImage"/>),
    /// null when the file can't be decoded (ImageFileView then shows the "open externally"
    /// fallback). Built on first bind, NOT in FromLoad — it touches disk / the decoder.</summary>
    public IImage? ImageSource
    {
        get
        {
            if (!_imageBuilt)
            {
                _imageBuilt = true;
                if (IsImage && FilePath is { } path)
                    _imageSource = LoadImage(path);
            }

            return _imageSource;
        }
    }

    private static IImage? LoadImage(string path)
    {
        try
        {
            return ImageFile.IsSvgExtension(path)
                ? new SvgImage { Source = SvgSource.Load(path, null, null) }
                : new Bitmap(path);
        }
        catch
        {
            return null; // unreadable / unsupported encoding → graceful fallback
        }
    }

    /// <summary>Show the image view (the source/preview/table hosts stay hidden for an image tab).</summary>
    public bool ShowImage => Kind == FileLoadKind.Image;

    /// <summary>Per-view image zoom factor (1 = 100%); only used when <see cref="ImageFit"/> is off.</summary>
    [ObservableProperty]
    private double _imageZoom = 1;

    /// <summary>True = fit the image to the window (default); false = a free/100% zoom.</summary>
    [ObservableProperty]
    private bool _imageFit = true;

    /// <summary>Image rotation in degrees (0/90/180/270).</summary>
    [ObservableProperty]
    private int _imageRotation;

    /// <summary>Toggle fit-to-window ⟷ 100%. Either way the free zoom resets to 1 (fit ignores it;
    /// 100% lands at actual size, and Ctrl+wheel adjusts from there).</summary>
    [RelayCommand]
    private void ToggleImageFit()
    {
        ImageFit = !ImageFit;
        ImageZoom = 1;
    }

    /// <summary>Rotate the image 90° clockwise (wraps at 360).</summary>
    [RelayCommand]
    private void RotateImage() => ImageRotation = (ImageRotation + 90) % 360;

    private CsvTableViewModel? _csvTable;
    private bool _csvTableBuilt;

    /// <summary>Sortable table model for .csv/.tsv tabs and key/value config files
    /// (.toml/.ini/.env → Ключ/Значение); null when the file doesn't parse (the source view shows
    /// instead). Cached: the document text is immutable.</summary>
    public CsvTableViewModel? CsvTable
    {
        get
        {
            if (!_csvTableBuilt)
            {
                _csvTableBuilt = true;
                var table = Delimiter is { } delimiter
                    ? DelimitedTable.Parse(DocumentText, delimiter)
                    : IsKeyValueConfig ? KeyValueConfig.Parse(DocumentText) : null;
                if (table is not null)
                    _csvTable = new CsvTableViewModel(table);
            }

            return _csvTable;
        }
    }

    /// <summary>Per-tab table-vs-source choice; new tabs inherit the persisted default.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCsvTable))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ShowSourcePane))]
    [NotifyPropertyChangedFor(nameof(ShowMinimap))]
    [NotifyPropertyChangedFor(nameof(ZoomApplies))]
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

    /// <summary>Per-tab pretty-print («формат») state for JSON/XML/NDJSON; new tabs inherit the
    /// persisted default from <see cref="Shared.EditorOptions.JsonPretty"/> when the shell adopts
    /// them (the persist field keeps its legacy name for settings.json compatibility).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText))]
    [NotifyPropertyChangedFor(nameof(IsSourceTransformActive))]
    private bool _prettyPrintEnabled;

    private string? _prettyText;
    private string? _smartText;

    /// <summary>True for prose-text files (.txt/.log) — they get the text outline and the
    /// display-only smart typography (ported).</summary>
    public bool IsPlainText => GrammarExtension is ".txt" or ".log";

    /// <summary>Per-tab smart-typography state; inherits the persisted default on adoption.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceText))]
    [NotifyPropertyChangedFor(nameof(IsSourceTransformActive))]
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
    /// pretty-printed JSON/XML/NDJSON, or smart typography for prose text. The raw text stays the
    /// single source of truth: exports, reload and search see the file as written.</summary>
    public string SourceText =>
        IsPrettyPrintable && PrettyPrintEnabled
            ? _prettyText ??= FormatPretty() ?? DocumentText
        : IsPlainText && SmartTypographyEnabled
            ? _smartText ??= SmartTypography.Apply(DocumentText)
        : DocumentText;

    /// <summary>Run the type-appropriate pretty-printer over the raw text (null → show raw).</summary>
    private string? FormatPretty() =>
        IsJson ? JsonPrettyPrinter.TryFormat(DocumentText)
        : IsXml ? XmlPrettyPrinter.TryFormat(DocumentText)
        : IsNdjson ? NdjsonPrettyPrinter.TryFormat(DocumentText)
        : null;

    /// <summary>True while the source editor shows a DISPLAY transform (pretty-JSON or smart
    /// typography) instead of the raw file. In-place editing is suppressed in this state so a
    /// save can never persist the transformed buffer back over the file — the transform
    /// (re-indentation, «guillemets», em-dashes) is lossy/irreversible. Toggling the transform
    /// off restores the raw text and re-enables editing.</summary>
    public bool IsSourceTransformActive =>
        (IsPrettyPrintable && PrettyPrintEnabled) || (IsPlainText && SmartTypographyEnabled);

    // Any path that flips a transform (toolbar toggle, EditorOptions adoption, settings import)
    // refreshes the status hint, so the read-only-under-transform state is always discoverable and
    // the plain encoding·EOL status returns the moment the transform is off.
    partial void OnPrettyPrintEnabledChanged(bool value) => RefreshTransformStatus();
    partial void OnSmartTypographyEnabledChanged(bool value) => RefreshTransformStatus();

    private void RefreshTransformStatus()
        => StatusText = IsSourceTransformActive
            ? "Только чтение: трансформация включена — выключите её для редактирования"
            : BuildStatus();

    /// <summary>Toggle pretty-print (JSON/XML/NDJSON) for this tab (and remember it as the default).</summary>
    [RelayCommand]
    private void TogglePrettyPrint()
    {
        PrettyPrintEnabled = !PrettyPrintEnabled;
        if (Editor is not null)
            Editor.JsonPretty = PrettyPrintEnabled;
    }

    /// <summary>Preview vs source. Defaults to Preview; only meaningful for markdown
    /// (for code files <see cref="ShowSource"/> short-circuits to true regardless).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPreview))]
    [NotifyPropertyChangedFor(nameof(ShowSource))]
    [NotifyPropertyChangedFor(nameof(ShowSplit))]
    [NotifyPropertyChangedFor(nameof(ShowSourcePane))]
    [NotifyPropertyChangedFor(nameof(ShowPreviewPane))]
    [NotifyPropertyChangedFor(nameof(ShowMinimap))]
    [NotifyPropertyChangedFor(nameof(ZoomApplies))]
    private DocumentViewMode _viewMode = DocumentViewMode.Preview;

    /// <summary>Show the rendered markdown preview (markdown file in Preview mode, has content).</summary>
    public bool ShowPreview => !ShowNotice && IsMarkdown && ViewMode == DocumentViewMode.Preview;

    /// <summary>Show the source editor (any non-markdown file, or markdown in Source mode),
    /// unless the tab is showing its table view or is a PDF.</summary>
    public bool ShowSource =>
        !ShowNotice && !ShowPdf && !ShowImage && (!IsMarkdown || ViewMode == DocumentViewMode.Source) && !ShowCsvTable;

    /// <summary>Show source + preview side by side (markdown only). In split, both hosts are
    /// realized but <see cref="ShowSource"/>/<see cref="ShowPreview"/> are false — the panes are
    /// revealed by the split grid's track lengths, not by IsVisible (see DocumentView.SplitLayout).</summary>
    public bool ShowSplit => !ShowNotice && IsMarkdown && ViewMode == DocumentViewMode.Split;

    /// <summary>Source-pane host visibility: shown in Source mode and in Split. (The split grid sets
    /// the track length; this just keeps the host out of layout when it's not on screen.)</summary>
    public bool ShowSourcePane => ShowSource || ShowSplit;

    /// <summary>Preview-pane host visibility: shown in Preview mode and in Split.</summary>
    public bool ShowPreviewPane => ShowPreview || ShowSplit;

    /// <summary>Whether the font-size zoom controls apply to this tab — the source editor, the
    /// markdown preview (zoomed via a layout scale) or the PDF view (zoom → render width). False
    /// for the table view and notice overlays.</summary>
    public bool ZoomApplies => ShowSource || ShowPreview || ShowSplit || ShowPdf;

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

    /// <summary>A binary / too-large / empty document shows a notice instead of editor + preview.
    /// (A PDF is NOT a notice — it routes to <see cref="ShowPdf"/>.)</summary>
    public bool ShowNotice => Kind is FileLoadKind.Binary or FileLoadKind.TooLarge
        || (Kind == FileLoadKind.Text && DocumentText.Length == 0);

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
            ? MarkdownPreprocessor.Transform(DocumentText, BuildWikiResolver(), Diagrams?.Enabled ?? false)
            : "";

    /// <summary>Wiki-name resolver for the preprocessor and the HTML exporter: a sibling
    /// <c>&lt;name&gt;.md</c> next to this document. Null for path-less tabs (sample) — nothing
    /// resolves. File I/O lives here in the UI layer; Core only sees the delegate.</summary>
    internal Func<string, bool>? BuildWikiResolver()
        => AssetPathRoot is not { } root
            ? null
            : name => File.Exists(Path.Combine(root, name + ".md"));

    /// <summary>Flip preview ⟷ source. Only enabled for markdown documents.</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ToggleViewMode()
        => ViewMode = ViewMode == DocumentViewMode.Preview ? DocumentViewMode.Source : DocumentViewMode.Preview;

    /// <summary>Show the preview (segmented switch). Setting the same mode is a no-op.</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ShowPreviewMode() => ViewMode = DocumentViewMode.Preview;

    /// <summary>Show the source (segmented switch).</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ShowSourceMode() => ViewMode = DocumentViewMode.Source;

    /// <summary>Toggle the side-by-side split view (markdown only). Off → return to Preview
    /// (markdown's default reading state).</summary>
    [RelayCommand(CanExecute = nameof(IsMarkdown))]
    private void ToggleSplit()
        => ViewMode = ViewMode == DocumentViewMode.Split ? DocumentViewMode.Preview : DocumentViewMode.Split;

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

    /// <summary>Test seam: true once the immutable derived caches (outline + preview markdown) are
    /// materialised, so a test can assert <see cref="FromLoad"/> warmed them off the render path.</summary>
    internal bool DerivedCachesWarm => _outline is not null && _previewMarkdown is not null;

    /// <summary>Test seam (Q17): true once the CSV/TSV table parse has run, so a test can assert
    /// <see cref="FromLoad"/> built it off the render path instead of synchronously on first bind.</summary>
    internal bool CsvTableWarm => _csvTableBuilt;

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

    private IReadOnlyList<HeadingOutline> _breadcrumbs = [];

    /// <summary>Ancestor chain of the active heading (top-level first), shown as the
    /// breadcrumbs strip. Empty above the first heading and for non-markdown. P10: cached and
    /// recomputed only on an ordinal change (not per binding read), and the change notification is
    /// suppressed when the chain is value-equal to the last one (e.g. empty→empty for a headingless
    /// doc whose scroll-spy still nudges the ordinal).</summary>
    public IReadOnlyList<HeadingOutline> Breadcrumbs => _breadcrumbs;

    partial void OnActiveHeadingOrdinalChanged(int value)
    {
        var chain = MarkdownOutline.AncestorChain(Outline, value);
        if (!chain.SequenceEqual(_breadcrumbs))
        {
            _breadcrumbs = chain;
            OnPropertyChanged(nameof(Breadcrumbs));
        }

        // Reading a heading marks it visited (ported md-visited-*): the TOC unread dot fades.
        // Version bumps ONLY on a genuinely new visit — a revisit fires per scroll tick and
        // would otherwise recompute every visible TOC row's multi-bindings at scroll rate.
        if (FilePath is not null && value >= 0 && ViewState is { } store
            && store.MarkVisited(FilePath, value))
        {
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

    /// <summary>Editor-command surface wired by <c>DocumentView</c> (mirrors <see cref="EditorTextProvider"/>);
    /// null for tabs with no source editor (image / PDF). The line-operation commands route through it.</summary>
    public IEditorActions? EditorActions { get; set; }

    /// <summary>Apply a Notepad++-style line operation through the command-intent backbone. No-op when no
    /// editor surface is attached. One command, parameterized by <see cref="LineOp"/>, drives every entry.</summary>
    [RelayCommand]
    private void ApplyLineOp(LineOp op)
    {
        if (EditorActions is { } actions)
            DispatchRecorded(actions, new TransformLinesIntent(op));
    }

    // Record the intent into the shell's macro recorder (a no-op unless recording), then apply it.
    private void DispatchRecorded(IEditorActions actions, IEditorIntent intent)
    {
        Shell?.RecordIntent(intent);
        EditorCommandDispatcher.Apply(actions, intent);
    }

    /// <summary>Convert the document's line endings (LF / CRLF / CR) through the backbone. The label
    /// refreshes after the next save+reload re-detects the EOL. No-op without an editor.</summary>
    [RelayCommand]
    private void ApplyEol(Eol target)
    {
        if (EditorActions is { } actions)
            DispatchRecorded(actions, new ConvertEolIntent(target));
    }

    /// <summary>Target encoding for saving this tab (Ctrl+S). Defaults to UTF-8 (no BOM), matching the
    /// prior save policy; the Кодировка menu/palette picks another. The status label refreshes after the
    /// next save+reload re-detects the written encoding.</summary>
    [ObservableProperty]
    private string _saveEncodingName = SaveEncoding.Utf8;

    [RelayCommand]
    private void SetSaveEncoding(string name) => SaveEncodingName = name;

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

        // Warm the immutable derived caches NOW, off the render-critical path. Computing Outline /
        // PreviewMarkdown lazily on first bind happens during tab-selection's property-change
        // dispatch (UI thread, after the body already painted) — the outline/preview then land a
        // frame late, the "empty skeleton flash". The document text is immutable, so precomputing
        // here is correctness-free and makes HasOutline an O(1) read by first paint.
        _ = tab.Outline;
        _ = tab.PreviewMarkdown;
        // Q17: parse the CSV/TSV table here too (off-thread for big files via BuildTabAsync), not
        // synchronously in the getter on first UI bind. O(1) for non-delimited tabs.
        _ = tab.CsvTable;
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
        FileLoadKind.Pdf => $"PDF · {SizeText}",
        FileLoadKind.Image => $"Изображение · {SizeText}",
        _ when DocumentText.Length == 0 => "Пустой файл",
        _ => $"Строк: {TextMetrics.LineCount(DocumentText)}   ·   Символов: {TextMetrics.CharCount(DocumentText)}"
           + $"   ·   {EncodingName}"
           + (LineEnding.Length > 0 ? $"   ·   {LineEnding}" : "")
           + (HighlightSuppressed ? "   ·   без подсветки" : ""),
    };

    private const string Sample = @"// Tittle — нативный markdown/code viewer
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
   открой реальный файл командой:  Tittle C:\path\to\file.rs  */
";
}
