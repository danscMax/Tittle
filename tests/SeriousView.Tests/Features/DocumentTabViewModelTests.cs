using System.IO;
using SeriousView.Core.Documents;
using SeriousView.Core.Text;
using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

public class DocumentTabViewModelTests
{
    [Fact]
    public void FromFile_MarkdownFile_DefaultsToPreview()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");

        Assert.True(vm.IsMarkdown);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
        Assert.False(vm.ShowSource);
        Assert.True(vm.ToggleViewModeCommand.CanExecute(null));
    }

    [Fact]
    public void IsSourceTransformActive_TracksPrettyJson_AndDrivesAReadOnlyStatusHint()
    {
        var vm = DocumentTabViewModel.FromFile("{\"a\":1}", "/data/x.json");
        Assert.False(vm.IsSourceTransformActive);
        var baseStatus = vm.StatusText;

        vm.PrettyPrintEnabled = true;
        Assert.True(vm.IsSourceTransformActive);          // pretty-JSON transform is live
        Assert.Contains("Только чтение", vm.StatusText);  // the read-only state is surfaced

        vm.PrettyPrintEnabled = false;
        Assert.False(vm.IsSourceTransformActive);
        Assert.Equal(baseStatus, vm.StatusText);          // the plain encoding·EOL status returns
    }

    [Fact]
    public void IsSourceTransformActive_TracksSmartTypography_ForPlainText()
    {
        var vm = DocumentTabViewModel.FromFile("text -- dash", "/notes/a.txt");
        Assert.False(vm.IsSourceTransformActive);

        vm.SmartTypographyEnabled = true;
        Assert.True(vm.IsSourceTransformActive);

        // A markdown tab has no source transform regardless of the toggles.
        var md = DocumentTabViewModel.FromFile("# h", "/docs/r.md");
        md.PrettyPrintEnabled = true;
        md.SmartTypographyEnabled = true;
        Assert.False(md.IsSourceTransformActive);
    }

    [Fact]
    public void FromFile_CodeFile_IsSourceOnly()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.False(vm.IsMarkdown);
        Assert.True(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        Assert.False(vm.ToggleViewModeCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleViewMode_FlipsPreviewSource_ForMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Source, vm.ViewMode);
        Assert.True(vm.ShowSource);
        Assert.False(vm.ShowPreview);

        vm.ToggleViewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
        Assert.False(vm.ShowSource);
    }

    [Fact]
    public void SegmentedCommands_SetExplicitMode_ForMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");
        Assert.True(vm.ShowPreviewModeCommand.CanExecute(null));
        Assert.True(vm.ShowSourceModeCommand.CanExecute(null));

        vm.ShowSourceModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Source, vm.ViewMode);
        Assert.True(vm.ShowSource);

        vm.ShowSourceModeCommand.Execute(null); // re-setting the active mode is a no-op
        Assert.Equal(DocumentViewMode.Source, vm.ViewMode);

        vm.ShowPreviewModeCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.True(vm.ShowPreview);
    }

    [Fact]
    public void SegmentedCommands_Disabled_ForNonMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.False(vm.ShowPreviewModeCommand.CanExecute(null));
        Assert.False(vm.ShowSourceModeCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleSplit_FlipsSplitAndPreview_ForMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");
        Assert.True(vm.ToggleSplitCommand.CanExecute(null));

        vm.ToggleSplitCommand.Execute(null);
        Assert.Equal(DocumentViewMode.Split, vm.ViewMode);
        Assert.True(vm.ShowSplit);
        Assert.False(vm.ShowPreview);   // in split, the single-mode flags are false…
        Assert.False(vm.ShowSource);    // …the panes are revealed by the split grid, not IsVisible
        Assert.True(vm.ZoomApplies);    // zoom still applies in split

        vm.ToggleSplitCommand.Execute(null); // off → back to Preview (markdown's default)
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);
        Assert.False(vm.ShowSplit);
        Assert.True(vm.ShowPreview);
    }

    [Fact]
    public void ToggleSplit_Disabled_ForNonMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.False(vm.ToggleSplitCommand.CanExecute(null));
    }

    [Fact]
    public void ShowSplit_False_ForNoticeTab()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Binary(2048), "/img/pic.png");
        vm.ViewMode = DocumentViewMode.Split;

        Assert.True(vm.ShowNotice);
        Assert.False(vm.ShowSplit); // a notice overrides the split panes
    }

    [Fact]
    public void OpenSearch_MarkdownInSplit_StaysSplit()
    {
        // In split the source pane is already on screen, so find must NOT force a mode switch
        // (only a pure-Preview tab is flipped to Source).
        var vm = DocumentTabViewModel.FromFile("# Title\n\nfind me", "/doc.md");
        vm.ViewMode = DocumentViewMode.Split;

        vm.OpenSearchCommand.Execute(null);

        Assert.True(vm.IsSearchOpen);
        Assert.Equal(DocumentViewMode.Split, vm.ViewMode);
    }

    [Fact]
    public void ZoomApplies_ForMarkdownPreviewAndSource_AndAnyCode()
    {
        var md = DocumentTabViewModel.FromFile("# Title", "/docs/readme.md");
        Assert.True(md.ShowPreview);
        Assert.True(md.ZoomApplies); // preview is zoomed via a layout scale

        md.ToggleViewModeCommand.Execute(null);
        Assert.True(md.ShowSource);
        Assert.True(md.ZoomApplies);

        var code = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");
        Assert.True(code.ZoomApplies);
    }

    [Fact]
    public void ZoomApplies_False_ForNoticeTab()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Binary(2048), "/img/pic.png");

        Assert.True(vm.ShowNotice);
        Assert.False(vm.ZoomApplies);
    }

    [Fact]
    public void ZoomApplies_False_InCsvTableView_AndNotifiesOnToggle()
    {
        var vm = DocumentTabViewModel.FromFile("a,b\n1,2\n3,4", "/data/rows.csv");
        vm.CsvAsTableEnabled = true;
        Assert.True(vm.ShowCsvTable);
        Assert.False(vm.ZoomApplies); // the table view isn't font-zoomed

        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(DocumentTabViewModel.ZoomApplies);
        vm.CsvAsTableEnabled = false; // flip to source → zoom applies again
        Assert.True(raised);
        Assert.True(vm.ZoomApplies);
    }

    [Fact]
    public void AssetPathRoot_IsDocumentDirectory()
    {
        const string path = "/docs/sub/readme.md";
        var vm = DocumentTabViewModel.FromFile("# Title", path);

        Assert.Equal(Path.GetDirectoryName(path), vm.AssetPathRoot);
    }

    [Fact]
    public void AssetPathRoot_Null_WhenNoFilePath()
    {
        var vm = DocumentTabViewModel.CreateSample();

        Assert.Null(vm.AssetPathRoot);
    }

    [Fact]
    public void PreviewMarkdown_PassesPlainTextThrough()
    {
        const string text = "# Title\n\nPlain paragraph with **bold**.";
        var vm = DocumentTabViewModel.FromFile(text, "/docs/readme.md");

        Assert.Equal(text, vm.PreviewMarkdown);
    }

    [Fact]
    public void PreviewMarkdown_Empty_ForNonMarkdown()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        Assert.Equal("", vm.PreviewMarkdown);
    }

    [Fact]
    public void Outline_ForMarkdownWithHeadings_IsPopulated()
    {
        var vm = DocumentTabViewModel.FromFile("# First\n\ntext\n\n## Second", "/docs/readme.md");

        Assert.True(vm.HasOutline);
        Assert.Equal(2, vm.Outline.Count);
        Assert.Equal("First", vm.Outline[0].Text);
        Assert.Equal("Second", vm.Outline[1].Text);
    }

    [Fact]
    public void FromLoad_WarmsDerivedCaches_OffTheRenderPath()
    {
        // Outline + preview markdown must be materialised by construction, so first paint doesn't
        // parse them lazily on the UI thread during tab-selection (the "empty skeleton flash").
        var vm = DocumentTabViewModel.FromFile("# First\n\ntext\n\n## Second", "/docs/readme.md");

        Assert.True(vm.DerivedCachesWarm);
    }

    [Fact]
    public void FromLoad_WarmsCsvTable_OffTheRenderPath()
    {
        // Q17: the CSV/TSV parse (up to 10k rows) must run in FromLoad (off-thread for big files via
        // BuildTabAsync), not synchronously in the getter on first UI bind.
        var vm = DocumentTabViewModel.FromFile("a,b\n1,2\n3,4", "/data/t.csv");

        Assert.True(vm.CsvTableWarm);
        Assert.NotNull(vm.CsvTable);
    }

    [Fact]
    public void Outline_ForCodeFile_IsEmpty()
    {
        // A '#'-prefixed line in a non-markdown file must not produce an outline.
        var vm = DocumentTabViewModel.FromFile("# not markdown", "/src/a.cs");

        Assert.False(vm.HasOutline);
        Assert.Empty(vm.Outline);
    }

    [Fact]
    public void NavigateToHeading_RaisesNavigationRequested_WithTheHeading()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n## B", "/docs/readme.md");
        HeadingOutline? received = null;
        vm.NavigationRequested += h => received = h;

        var target = vm.Outline[1];
        vm.NavigateToHeadingCommand.Execute(target);

        Assert.Same(target, received);
    }

    [Fact]
    public void FromLoad_Binary_ShowsNotice_HidesEditorAndPreview()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Binary(2048), "/img/pic.png");

        Assert.True(vm.ShowNotice);
        Assert.False(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        Assert.Contains("Бинарный", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_TooLarge_ShowsNotice()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.TooLarge(60L * 1024 * 1024), "/big.txt");

        Assert.True(vm.ShowNotice);
        Assert.Contains("слишком большой", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_EmptyFile_ShowsNotice()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.ForText("", "UTF-8", "", 0), "/empty.md");

        Assert.True(vm.ShowNotice);
        Assert.Equal("Файл пуст", vm.NoticeText);
    }

    [Fact]
    public void FromLoad_Text_StatusShowsEncodingAndEol()
    {
        var vm = DocumentTabViewModel.FromLoad(
            FileLoadResult.ForText("a\nb", "Windows-1251", "LF", 3), "/doc.txt");

        Assert.False(vm.ShowNotice);
        Assert.Contains("Windows-1251", vm.StatusText);
        Assert.Contains("LF", vm.StatusText);
    }

    [Fact]
    public void FromLoad_BigText_SuppressesHighlight()
    {
        var vm = DocumentTabViewModel.FromLoad(
            FileLoadResult.ForText("x", "UTF-8", "LF", 10L * 1024 * 1024), "/big.cs");

        Assert.True(vm.HighlightSuppressed);
        Assert.Contains("без подсветки", vm.StatusText);
    }

    [Fact]
    public void CaretText_FormatsLineAndColumn_AndUpdates()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb", "/src/a.cs");
        Assert.Equal("Стр 1, Кол 1", vm.CaretText);

        vm.CaretLine = 5;
        vm.CaretColumn = 12;
        Assert.Equal("Стр 5, Кол 12", vm.CaretText);
    }

    [Fact]
    public void SubmitGoToLine_ValidLine_RaisesRequest_AndCloses()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb\nc\nd\ne", "/src/a.cs");
        vm.IsGoToLineOpen = true;
        vm.GoToLineText = "3";
        int? got = null;
        vm.GoToLineRequested += l => got = l;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.Equal(3, got);
        Assert.False(vm.IsGoToLineOpen);
        Assert.Equal("", vm.GoToLineText);
    }

    [Fact]
    public void SubmitGoToLine_ClampsToDocumentBounds()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb\nc", "/src/a.cs"); // 3 lines
        vm.GoToLineText = "999";
        int? got = null;
        vm.GoToLineRequested += l => got = l;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.Equal(3, got);
    }

    [Fact]
    public void SubmitGoToLine_NonNumeric_DoesNotRaise_ButCloses()
    {
        var vm = DocumentTabViewModel.FromFile("a\nb", "/src/a.cs");
        vm.IsGoToLineOpen = true;
        vm.GoToLineText = "abc";
        var raised = false;
        vm.GoToLineRequested += _ => raised = true;

        vm.SubmitGoToLineCommand.Execute(null);

        Assert.False(raised);
        Assert.False(vm.IsGoToLineOpen);
    }

    [Fact]
    public void OpenSearch_CodeFile_OpensTheBar()
    {
        var vm = DocumentTabViewModel.FromFile("alpha beta", "/a.cs");

        vm.OpenSearchCommand.Execute(null);

        Assert.True(vm.IsSearchOpen);
    }

    [Fact]
    public void SearchQuery_ComputesMatches_AndJumpsToFirst()
    {
        var vm = DocumentTabViewModel.FromFile("alpha beta alpha gamma alpha", "/a.cs");

        vm.SearchQuery = "alpha";

        Assert.Equal(3, vm.SearchMatchCount);
        Assert.Equal(0, vm.SearchCurrentIndex);
        Assert.Equal("1 / 3", vm.SearchStatus);
    }

    [Fact]
    public void NextAndPreviousMatch_CycleWithWrap()
    {
        var vm = DocumentTabViewModel.FromFile("x x x", "/a.cs"); // matches at 0, 2, 4
        vm.SearchQuery = "x";
        Assert.Equal(0, vm.SearchCurrentIndex);

        vm.NextMatchCommand.Execute(null);
        Assert.Equal(1, vm.SearchCurrentIndex);
        vm.NextMatchCommand.Execute(null);
        Assert.Equal(2, vm.SearchCurrentIndex);
        vm.NextMatchCommand.Execute(null);
        Assert.Equal(0, vm.SearchCurrentIndex);   // wrap to first

        vm.PreviousMatchCommand.Execute(null);
        Assert.Equal(2, vm.SearchCurrentIndex);   // wrap to last
    }

    [Fact]
    public void ToggleSearchCaseSensitive_NarrowsMatches()
    {
        var vm = DocumentTabViewModel.FromFile("Foo foo FOO", "/a.cs");
        vm.SearchQuery = "foo";
        Assert.Equal(3, vm.SearchMatchCount); // case-insensitive by default

        vm.ToggleSearchCaseSensitiveCommand.Execute(null);

        Assert.True(vm.SearchCaseSensitive);
        Assert.Equal(1, vm.SearchMatchCount);
    }

    [Fact]
    public void SearchRegex_Invalid_FlagsError_AndClearsMatches()
    {
        var vm = DocumentTabViewModel.FromFile("anything", "/a.cs");
        vm.SearchRegex = true;
        vm.SearchQuery = "[unclosed";

        Assert.True(vm.SearchInvalidRegex);
        Assert.Equal(0, vm.SearchMatchCount);
        Assert.Equal("ошибка", vm.SearchStatus);
    }

    [Fact]
    public void OpenSearch_MarkdownInPreview_SwitchesToSource()
    {
        var vm = DocumentTabViewModel.FromFile("# Title\n\nfind me", "/doc.md");
        Assert.Equal(DocumentViewMode.Preview, vm.ViewMode);

        vm.OpenSearchCommand.Execute(null);

        Assert.True(vm.IsSearchOpen);
        Assert.Equal(DocumentViewMode.Source, vm.ViewMode); // find runs over the source → show it
        Assert.True(vm.ShowSource);
    }

    [Fact]
    public void OpenSearch_NoticeTab_DoesNotOpen()
    {
        var vm = DocumentTabViewModel.FromLoad(FileLoadResult.Binary(2048), "/img.png");

        vm.OpenSearchCommand.Execute(null);

        Assert.False(vm.IsSearchOpen);
    }

    [Fact]
    public void CloseSearch_ClearsMatchesAndState()
    {
        var vm = DocumentTabViewModel.FromFile("x x x", "/a.cs");
        vm.SearchQuery = "x";
        Assert.Equal(3, vm.SearchMatchCount);

        vm.CloseSearchCommand.Execute(null);

        Assert.False(vm.IsSearchOpen);
        Assert.Equal(0, vm.SearchMatchCount);
        Assert.Equal(-1, vm.SearchCurrentIndex);
        Assert.Empty(vm.SearchMatches);
    }

    [Fact]
    public void PreviewMarkdown_ResolvesWikiLinks_AgainstSiblingNotes()
    {
        var root = System.IO.Directory.CreateTempSubdirectory("sv-tab-wiki-").FullName;
        try
        {
            System.IO.File.WriteAllText(Path.Combine(root, "note.md"), "# n");

            var with = DocumentTabViewModel.FromFile("see [[note]] and [[gone]]", Path.Combine(root, "doc.md"));

            Assert.Contains("[note](wiki:note)", with.PreviewMarkdown);
            Assert.Contains("and gone", with.PreviewMarkdown); // missing sibling → plain text
        }
        finally
        {
            System.IO.Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Breadcrumbs_FollowTheActiveHeading_AndNotify()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n## B\n### C", "/docs/d.md");
        var raised = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentTabViewModel.Breadcrumbs))
                raised++;
        };

        vm.ActiveHeadingOrdinal = 2;

        Assert.Equal(1, raised);
        Assert.Equal(new[] { "A", "B", "C" }, System.Linq.Enumerable.Select(vm.Breadcrumbs, h => h.Text));

        vm.ActiveHeadingOrdinal = -1;
        Assert.Empty(vm.Breadcrumbs);
    }

    [Fact]
    public void Breadcrumbs_HeadinglessDoc_OrdinalNudge_DoesNotNotify()
    {
        // P10: a doc with no headings has an empty ancestor chain, so a scroll-spy ordinal nudge
        // produces the same empty chain and must NOT re-raise Breadcrumbs (no notify, no re-alloc).
        var vm = DocumentTabViewModel.FromFile("just paragraphs\n\nno headings here", "/docs/d.md");
        Assert.Empty(vm.Outline);
        var before = vm.Breadcrumbs;
        var raised = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentTabViewModel.Breadcrumbs))
                raised++;
        };

        vm.ActiveHeadingOrdinal = 3; // out of range → empty chain again
        vm.ActiveHeadingOrdinal = 5;

        Assert.Equal(0, raised);
        Assert.Same(before, vm.Breadcrumbs); // same cached instance — no allocation
    }

    [Fact]
    public void Breadcrumbs_CachedBetweenReads_RecomputedOnlyOnRealChange()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n## B", "/docs/d.md");
        vm.ActiveHeadingOrdinal = 1;

        var first = vm.Breadcrumbs;
        Assert.Same(first, vm.Breadcrumbs); // P10: cached — reading twice does not re-allocate

        vm.ActiveHeadingOrdinal = 0;
        Assert.NotSame(first, vm.Breadcrumbs); // recomputed on a genuine ordinal change
        Assert.Equal(new[] { "A" }, System.Linq.Enumerable.Select(vm.Breadcrumbs, h => h.Text));
    }

    // --- Added formats: XML / NDJSON pretty-print + TOML/INI/.env metadata table ---

    [Theory]
    [InlineData("/a/x.xml", true)]
    [InlineData("/a/x.csproj", true)]
    [InlineData("/a/x.axaml", true)]
    [InlineData("/a/x.svg", false)] // images deferred — .svg is NOT XML pretty-printed
    [InlineData("/a/x.cs", false)]
    public void IsXml_TracksXmlFamilyExtensions(string path, bool expected)
        => Assert.Equal(expected, DocumentTabViewModel.FromFile("<r/>", path).IsXml);

    [Fact]
    public void IsPrettyPrintable_CoversJsonXmlNdjson_AndDispatchesSourceText()
    {
        var xml = DocumentTabViewModel.FromFile("<root><a>1</a></root>", "/a/x.xml");
        Assert.True(xml.IsPrettyPrintable);
        xml.PrettyPrintEnabled = true;
        Assert.Contains("\n  <a>1</a>", xml.SourceText); // XML pretty-printer ran via SourceText
        Assert.True(xml.IsSourceTransformActive);

        var ndjson = DocumentTabViewModel.FromFile("{\"a\":1}\n{\"b\":2}", "/a/x.jsonl");
        Assert.True(ndjson.IsPrettyPrintable);
        ndjson.PrettyPrintEnabled = true;
        Assert.Contains("\"a\": 1", ndjson.SourceText);
        Assert.Contains("\"b\": 2", ndjson.SourceText);

        Assert.False(DocumentTabViewModel.FromFile("x", "/a/x.cs").IsPrettyPrintable);
    }

    [Fact]
    public void KeyValueConfig_RendersAsTable_ReusingTheCsvOverlay()
    {
        var vm = DocumentTabViewModel.FromFile("[server]\nhost = localhost\nport = 8080", "/a/app.toml");

        Assert.True(vm.IsKeyValueConfig);
        Assert.NotNull(vm.CsvTable); // parsed into the shared table model
        Assert.Equal(["Ключ", "Значение"], System.Linq.Enumerable.Select(vm.CsvTable!.Columns, c => c.Header));

        vm.CsvAsTableEnabled = true;
        Assert.True(vm.ShowCsvTable);
        Assert.False(vm.ShowSource); // table replaces the source view
    }
}
