using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using SeriousView.Core.Text;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

public class DocumentViewTests
{
    // Representative GFM (headings, emphasis, strikethrough, link, task list, table,
    // a tiny code fence) — deliberately image-free so headless rendering stays stable.
    private const string Sample = """
        # Heading

        Text with **bold**, _italic_, ~~strike~~ and a [link](https://example.com).

        - [x] done
        - [ ] todo

        | A | B |
        |---|---|
        | 1 | 2 |

        ```cs
        var x = 1;
        ```
        """;

    [AvaloniaFact]
    public void DocumentView_MarkdownPreview_RendersWithoutThrowing()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };

        window.Show();                  // applies bindings + lays out → engine parses markdown
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowPreview);    // markdown defaults to the rendered preview
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_ReadingModeOn_RendersWithoutThrowing()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        vm.Layout = new LayoutOptions { ReadingMode = true }; // centered column + decor + width/padding converters
        var window = new Window { Content = new DocumentView { DataContext = vm } };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowPreview);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_CodeFile_ShowsSourceNotPreview()
    {
        var vm = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowSource);
        Assert.False(vm.ShowPreview);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_NavigateToHeading_DoesNotThrow()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n\ntext\n\n## B", "/docs/readme.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Preview in-place scroll if the heading control is found, else source fallback —
        // either path must complete without throwing.
        vm.NavigateToHeadingCommand.Execute(vm.Outline[1]);
        Dispatcher.UIThread.RunJobs();

        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_RelaysCaretPosition_IntoTheTabVm()
    {
        var vm = DocumentTabViewModel.FromFile("line1\nline2\nline3", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        editor.TextArea.Caret.Line = 3;
        editor.TextArea.Caret.Column = 2;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, vm.CaretLine);
        Assert.Equal(2, vm.CaretColumn);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_GoToLine_ScrollsTheEditorToThatLine()
    {
        var vm = DocumentTabViewModel.FromFile("l1\nl2\nl3\nl4\nl5", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.GoToLineText = "4";
        vm.SubmitGoToLineCommand.Execute(null); // → GoToLineRequested(4) → DocumentView scrolls
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.Equal(4, editor.TextArea.Caret.Line);
        window.Close();
    }
    // NB: auto-focus (#29) can't be asserted headlessly (the headless window isn't activated, so
    // Focus() leaves IsFocused false) — it's verified live instead (keyboard scrolls without a click).

    /// <summary>~5 H1 headings with 20 paragraphs between — long enough that every heading
    /// needs real scrolling in a 400px-tall window (used by the M10 scroll tests).</summary>
    private static string LongMarkdown()
    {
        var sb = new StringBuilder();
        for (var h = 0; h < 5; h++)
        {
            sb.AppendLine($"# Heading {h}");
            sb.AppendLine();
            for (var p = 0; p < 20; p++)
            {
                sb.AppendLine($"Paragraph {h}.{p} with enough words to take a visible line.");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    /// <summary>Replica of the view's heading walk: content-space Y per heading ordinal.</summary>
    private static System.Collections.Generic.List<double> PreviewHeadingTops(ScrollViewer scroll, Window window)
        => window.GetVisualDescendants().OfType<Control>()
            .Where(c => c.Classes.Any(cl => cl.StartsWith("Heading", StringComparison.Ordinal)))
            .Select(c => c.TranslatePoint(default, scroll)!.Value.Y + scroll.Offset.Y)
            .ToList();

    private static ScrollViewer PreviewScrollOf(Window window)
        => window.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Name == "PreviewScroll");

    /// <summary>Window for scroll tests: small enough that the long fixture overflows. The
    /// scrollbars are forced Hidden — an overflowing headless window otherwise materialises the
    /// FluentAvalonia scrollbar chevron FontIcons, and shaping their embedded Symbols font
    /// crashes headless (project memory). Hidden bars are never measured; Offset/Extent (and so
    /// all scroll maths) keep working.</summary>
    private static Window CreateScrollTestWindow(DocumentTabViewModel vm)
        => CreateScrollTestWindow(new DocumentView { DataContext = vm });

    private static Window CreateScrollTestWindow(DocumentView view)
    {
        view.PreviewScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.PreviewScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.Source.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        view.Source.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        return new Window { Width = 800, Height = 400, Content = view };
    }

    [AvaloniaFact]
    public void NavigateToHeading_Preview_ScrollsHeadingToTheTop()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[2]);
        Dispatcher.UIThread.RunJobs();

        var scroll = PreviewScrollOf(window);
        var tops = PreviewHeadingTops(scroll, window);
        Assert.True(scroll.Offset.Y > 0);
        // The heading sits Padding.Top below the viewport top (the document-start look).
        Assert.True(Math.Abs(scroll.Offset.Y - (tops[2] - scroll.Padding.Top)) <= 1,
            $"offset {scroll.Offset.Y:0.##} vs expected {tops[2] - scroll.Padding.Top:0.##}");
        window.Close();
    }

    [AvaloniaFact]
    public void NavigateToHeading_Source_LineLandsAtTheViewportTop()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.ViewMode = DocumentViewMode.Source;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[3]);
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.Equal(vm.Outline[3].Line, editor.TextArea.Caret.Line);
        var expected = editor.TextArea.TextView.GetVisualTopByDocumentLine(vm.Outline[3].Line);
        Assert.True(Math.Abs(editor.VerticalOffset - expected) <= 1,
            $"offset {editor.VerticalOffset:0.##} vs line top {expected:0.##}, " +
            $"extent {editor.ExtentHeight:0.##}, viewport {editor.ViewportHeight:0.##}");
        window.Close();
    }

    [AvaloniaFact]
    public void NavigateToHeading_FirstHeading_ClampsToZeroOffset()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[3]); // scroll away first
        Dispatcher.UIThread.RunJobs();
        vm.NavigateToHeadingCommand.Execute(vm.Outline[0]); // back to the top heading
        Dispatcher.UIThread.RunJobs();

        var scroll = PreviewScrollOf(window);
        var tops = PreviewHeadingTops(scroll, window);
        // The first heading carries its own top margin, so "top of document" means the clamped
        // formula value (a few px), not a literal zero.
        var expected = Math.Max(0, tops[0] - scroll.Padding.Top);
        Assert.True(Math.Abs(scroll.Offset.Y - expected) <= 1,
            $"offset {scroll.Offset.Y:0.##} vs expected {expected:0.##}");
        Assert.True(scroll.Offset.Y <= 10, $"offset {scroll.Offset.Y:0.##} should be ~document top");
        window.Close();
    }

    private static int FirstVisibleLine(TextEditor editor)
    {
        var tv = editor.TextArea.TextView;
        return tv.GetDocumentLineByVisualTop(tv.ScrollOffset.Y + 1).LineNumber;
    }

    [AvaloniaFact]
    public void ToggleViewMode_PreviewToSource_LandsOnTheAnchoredHeadingLine()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[2]); // heading 2 at the preview top
        Dispatcher.UIThread.RunJobs();
        vm.ToggleViewModeCommand.Execute(null);             // → source; sync is posted
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.InRange(FirstVisibleLine(editor), vm.Outline[2].Line - 1, vm.Outline[2].Line + 1);
        window.Close();
    }

    [AvaloniaFact]
    public void ToggleViewMode_SourceToPreview_MonotoneAcrossHeadings()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.ViewMode = DocumentViewMode.Source;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        double OffsetAfterToggleFrom(int ordinal)
        {
            vm.ViewMode = DocumentViewMode.Source;
            Dispatcher.UIThread.RunJobs();
            vm.NavigateToHeadingCommand.Execute(vm.Outline[ordinal]);
            Dispatcher.UIThread.RunJobs();
            vm.ViewMode = DocumentViewMode.Preview;
            Dispatcher.UIThread.RunJobs();
            return PreviewScrollOf(window).Offset.Y;
        }

        var atFirst = OffsetAfterToggleFrom(1);
        var atThird = OffsetAfterToggleFrom(3);

        Assert.True(atThird > atFirst, $"offset {atThird:0.##} should be below {atFirst:0.##}");
        Assert.True(atFirst > 0, "a mid-document heading must not land at the very top");
        window.Close();
    }

    [AvaloniaFact]
    public void ToggleViewMode_RoundTrip_StaysOnTheHeadingLine()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[2]);
        Dispatcher.UIThread.RunJobs();

        for (var i = 0; i < 2; i++) // preview→source→preview→source
        {
            vm.ToggleViewModeCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
        }
        vm.ToggleViewModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.InRange(FirstVisibleLine(editor), vm.Outline[2].Line - 1, vm.Outline[2].Line + 1);
        window.Close();
    }

    [AvaloniaFact]
    public void ToggleViewMode_NoHeadings_DoesNotThrow()
    {
        var vm = DocumentTabViewModel.FromFile("plain text\n\nwithout any headings", "/docs/plain.md");
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.ToggleViewModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        vm.ToggleViewModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.ShowPreview);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_InitiallyZero_WhenTheDocStartsWithAHeading()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, vm.ActiveHeadingOrdinal);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_MinusOne_WhenProseComesFirst()
    {
        var md = "intro prose before any heading\n\nmore intro\n\n" + LongMarkdown();
        var vm = DocumentTabViewModel.FromFile(md, "/docs/long.md");
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(-1, vm.ActiveHeadingOrdinal);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_Source_FollowsGoToLine()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.ViewMode = DocumentViewMode.Source;
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.GoToLineText = (vm.Outline[3].Line + 2).ToString();
        vm.SubmitGoToLineCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, vm.ActiveHeadingOrdinal);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_CodeTab_FollowsGoToLine()
    {
        // Ported "code breadcrumbs": the scroll-spy must drive the marker + crumbs from the
        // SYMBOL outline of a code tab, not just markdown headings.
        var code = new StringBuilder("public class First\n{\n");
        for (var i = 0; i < 60; i++)
            code.AppendLine($"    // filler {i}");
        code.AppendLine("    public void Second()\n    {\n    }");
        for (var i = 0; i < 60; i++)
            code.AppendLine($"    // tail {i}");
        code.AppendLine("}");

        var vm = DocumentTabViewModel.FromFile(code.ToString(), "/src/big.cs");
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.Outline.Count); // class + method

        vm.GoToLineText = (vm.Outline[1].Line + 2).ToString();
        vm.SubmitGoToLineCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, vm.ActiveHeadingOrdinal);
        Assert.Equal(new[] { "First", "Second" }, vm.Breadcrumbs.Select(b => b.Text).ToArray());
        window.Close();
    }

    [AvaloniaFact]
    public void ScrollPercent_Source_FillsAfterAJumpDown()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.ViewMode = DocumentViewMode.Source;
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.GoToLineText = vm.Outline[^1].Line.ToString(); // deep into the document
        vm.SubmitGoToLineCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.EndsWith("%", vm.ScrollPercentText);
        Assert.NotEqual("0%", vm.ScrollPercentText);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_Preview_TocClickMarksTheClickedHeading()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[2]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.ActiveHeadingOrdinal);
        window.Close();
    }

    [AvaloniaFact]
    public void ActiveHeading_DoesNotChurn_OnEqualValue()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.IsActive = true;
        var view = new DocumentView { DataContext = vm };
        var window = CreateScrollTestWindow(view);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var changes = 0;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentTabViewModel.ActiveHeadingOrdinal))
                changes++;
        };

        view.RecomputeActiveHeading();
        view.RecomputeActiveHeading();

        Assert.Equal(0, changes); // already 0 after startup; equal recomputes raise nothing
        window.Close();
    }

    [AvaloniaFact]
    public void Breadcrumbs_StripShows_ForMarkdownWithHeadings_NotForCode()
    {
        var md = DocumentTabViewModel.FromFile("# A\n## B", "/docs/a.md");
        var cs = DocumentTabViewModel.FromFile("var x = 1;", "/src/a.cs");

        var window = new Window { Content = new DocumentView { DataContext = md } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var strip = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "BreadcrumbBar");
        Assert.True(strip.IsVisible);
        window.Close();

        window = new Window { Content = new DocumentView { DataContext = cs } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        strip = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "BreadcrumbBar");
        Assert.False(strip.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void Breadcrumbs_RenderTheChain_AndNavigateOnClick()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n\n## B\n\n### C", "/docs/a.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.ActiveHeadingOrdinal = 2; // ### C → chain A › B › C
        Dispatcher.UIThread.RunJobs();

        var crumbs = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("crumb")).ToList();
        Assert.Equal(new[] { "A", "B", "C" }, crumbs.Select(c => c.Content?.ToString()));

        HeadingOutline? navigated = null;
        vm.NavigationRequested += h => navigated = h;
        crumbs[0].Command!.Execute(crumbs[0].CommandParameter);

        Assert.Equal("A", navigated?.Text);
        window.Close();
    }

    /// <summary>A document that is mostly one giant fenced code block (the reported repro:
    /// a prompt file wrapped in ```markdown).</summary>
    private static string GiantFenceMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Title");
        sb.AppendLine();
        sb.AppendLine("Intro paragraph before the giant fence.");
        sb.AppendLine();
        sb.AppendLine("```markdown");
        for (var i = 1; i <= 120; i++)
            sb.AppendLine($"line {i} of the embedded prompt content");
        sb.AppendLine("```");
        return sb.ToString();
    }

    [AvaloniaFact]
    public void GiantFence_EmbeddedCodeEditor_RendersFullContentHeight()
    {
        var vm = DocumentTabViewModel.FromFile(GiantFenceMarkdown(), "/docs/prompt.md");
        var view = new DocumentView { DataContext = vm };
        var window = CreateScrollTestWindow(view);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The embedded code editor (Markdown.Avalonia SyntaxHigh) must show its WHOLE content.
        // Under our outer-scroll layout it gets an infinite height constraint it can't handle:
        // its desired height is an estimate and its extent reads ~double the truth — so the
        // fix pins height = lineCount × real line height, which this asserts against.
        var embedded = window.GetVisualDescendants().OfType<TextEditor>()
            .First(e => !ReferenceEquals(e, view.Source));

        var textView = embedded.TextArea.TextView;
        Assert.True(textView.VisualLinesValid && textView.VisualLines.Count > 0,
            "the block's visual lines should be materialised");
        var lineHeight = textView.VisualLines[0].Height; // the REAL rendered line height —
        // DefaultLineHeight/extent are height-tree estimates that read ~double of it here
        var contentHeight = 120 * lineHeight;
        Assert.True(embedded.Bounds.Height >= contentHeight,
            $"embedded editor height {embedded.Bounds.Height:0.#} must fit 120 lines × {lineHeight:0.#}");
        Assert.True(embedded.Bounds.Height <= contentHeight + 80,
            $"embedded editor height {embedded.Bounds.Height:0.#} must not trail an empty tail " +
            $"beyond the content {contentHeight:0.#}");
        Assert.False(double.IsInfinity(embedded.ViewportHeight),
            "the inner viewport must be finite — an infinite one clamps every scroll to 0");

        // With the full height granted, the whole-document scroll owns navigation.
        Assert.True(view.PreviewScroll.Extent.Height >= contentHeight,
            $"page extent {view.PreviewScroll.Extent.Height:0.#} must include the block {contentHeight:0.#}");
        window.Close();
    }

    // --- M14 C3: the reading position survives a reload (anchor handshake) ---

    [AvaloniaFact]
    public void Scrolling_WritesTheReadingAnchor()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.IsActive = true;
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        vm.NavigateToHeadingCommand.Execute(vm.Outline[2]); // scroll deep into the doc
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.ReadingAnchor.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void RestoreAnchor_Preview_LandsOnTheSameHeading()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.IsActive = true;
        vm.RestoreAnchor = new HeadingAnchor(2, 0);
        var view = new DocumentView { DataContext = vm };
        var window = CreateScrollTestWindow(view);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.RestoreAnchor); // one-shot: consumed by the fresh view
        Assert.True(view.PreviewScroll.Offset.Y > 0, "the restored view must not sit at the top");
        view.RecomputeActiveHeading();
        Assert.Equal(2, vm.ActiveHeadingOrdinal);
        window.Close();
    }

    [AvaloniaFact]
    public void RestoreAnchor_Source_LandsOnTheSameHeadingLine()
    {
        var vm = DocumentTabViewModel.FromFile(LongMarkdown(), "/docs/long.md");
        vm.ViewMode = DocumentViewMode.Source;
        vm.IsActive = true;
        vm.RestoreAnchor = new HeadingAnchor(3, 0);
        var window = CreateScrollTestWindow(vm);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.InRange(FirstVisibleLine(editor), vm.Outline[3].Line - 1, vm.Outline[3].Line + 1);
        window.Close();
    }

    // --- M11: block math in the preview ---

    [AvaloniaFact]
    public void MathBlock_RendersAsANativeMathView()
    {
        var vm = DocumentTabViewModel.FromFile("# T\n\n$$\nE = mc^2\n$$\n\ntext", "/docs/m.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var math = window.GetVisualDescendants().OfType<CSharpMath.Avalonia.MathView>().First();
        Assert.Null(math.ErrorMessage);
        Assert.True(math.Bounds.Height > 0, "the formula should measure");
        window.Close();
    }

    [AvaloniaFact]
    public void MathBlock_GarbageLatex_ShowsAnInlineError_NoCrash()
    {
        var vm = DocumentTabViewModel.FromFile("$$\n\\frac{unclosed\n$$", "/docs/m.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var math = window.GetVisualDescendants().OfType<CSharpMath.Avalonia.MathView>().First();
        Assert.NotNull(math.ErrorMessage); // surfaced inline by the control, never a crash
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_EditorContextMenu_HasCopySelectAllAndFind()
    {
        var vm = DocumentTabViewModel.FromFile("hello", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        var flyout = Assert.IsType<MenuFlyout>(editor.ContextFlyout);
        var headers = flyout.Items.OfType<MenuItem>().Select(i => i.Header?.ToString()).ToList();
        Assert.Equal(new[] { "Копировать", "Выделить всё", "Найти…" }, headers);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_EditorMenu_CopyEnablement_FollowsSelection()
    {
        var vm = DocumentTabViewModel.FromFile("hello", "/src/a.cs");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        var flyout = (MenuFlyout)editor.ContextFlyout!;
        var copy = flyout.Items.OfType<MenuItem>().First();

        // What flyout Opening calls — showing the real popup shapes the FluentAvalonia
        // Symbols font, which crashes headless (see project memory).
        view.RefreshEditorMenu();
        Assert.False(copy.IsEnabled); // no selection yet

        editor.SelectAll();
        view.RefreshEditorMenu();
        Assert.True(copy.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_EditorMenu_SelectAll_SelectsWholeDocument()
    {
        var vm = DocumentTabViewModel.FromFile("hello", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        var flyout = (MenuFlyout)editor.ContextFlyout!;
        var selectAll = flyout.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Выделить всё");

        selectAll.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("hello".Length, editor.SelectionLength);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_EditorMenu_Find_OpensTheSearchBar()
    {
        var vm = DocumentTabViewModel.FromFile("hello", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        var flyout = (MenuFlyout)editor.ContextFlyout!;
        var find = flyout.Items.OfType<MenuItem>().First(i => (string?)i.Header == "Найти…");

        find.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsSearchOpen);
        window.Close();
    }

    // ---- YAML front-matter panel (ported) ----

    [AvaloniaFact]
    public void FrontMatterPanel_RendersKeyValueRows()
    {
        var handler = new AdmonitionBlockHandler(new Markdown.Avalonia.Markdown());
        var encoded = Uri.EscapeDataString("title: Заметка\ntags: a, b");

        var border = handler.ProvideControl("", "frontmatter", encoded);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("frontmatter-block", border.Classes);
        var texts = border.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Метаданные", texts);
        Assert.Contains("title", texts);
        Assert.Contains("Заметка", texts);
        Assert.Contains("tags", texts);
        Assert.Contains("a, b", texts);
    }

    [AvaloniaFact]
    public void FrontMatterPanel_EndToEnd_ShowsInThePreview()
    {
        var vm = DocumentTabViewModel.FromFile("---\nauthor: Иван\n---\n# Doc", "/docs/readme.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("author", texts);
        Assert.Contains("Иван", texts);
        window.Close();
    }

    // ---- image lightbox (ported) ----

    [AvaloniaFact]
    public void Lightbox_OpensForAPreviewImage()
    {
        var vm = DocumentTabViewModel.FromFile("# t", "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
            new PixelSize(2, 2), new Vector(96, 96));
        var image = new Avalonia.Controls.Image { Source = bitmap };

        Assert.True(view.TryOpenLightbox(image));
        Dispatcher.UIThread.RunJobs();

        var lightbox = Assert.IsType<ImageLightboxWindow>(Assert.Single(window.OwnedWindows));
        Assert.Same(bitmap, lightbox.FindControl<Avalonia.Controls.Image>("ImageView")!.Source);
        lightbox.Close();
        window.Close();
    }

    [AvaloniaFact]
    public void Lightbox_IgnoresNonImageClicks()
    {
        var vm = DocumentTabViewModel.FromFile("# t", "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(view.TryOpenLightbox(new TextBlock()));
        Assert.Empty(window.OwnedWindows);
        window.Close();
    }

    // ---- copy button on preview code blocks (ported) ----

    [AvaloniaFact]
    public void DocumentView_PreviewCodeBlock_GetsACopyButton()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md"); // Sample has a ```cs fence
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Search under the preview only — the (hidden) source editor also contains the fence text.
        var preview = view.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("Preview")!;
        var embedded = preview.GetVisualDescendants().OfType<TextEditor>()
            .First(e => e.Text?.Contains("var x = 1;") == true);
        var copy = preview.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("code-copy")).ToList();

        Assert.Single(copy);
        // The wrap is marked and idempotent — a second fixup pass must not nest another grid.
        var host = Assert.IsType<Grid>(copy[0].Parent);
        Assert.Contains("code-copy-host", host.Classes);
        Assert.Single(preview.GetVisualDescendants().OfType<Grid>(),
            g => g.Classes.Contains("code-copy-host"));
        window.Close();
    }

    // ---- cv-* code decorations (ported) ----

    [AvaloniaFact]
    public void DocumentView_CodeTab_AttachesTheCvColorizer()
    {
        var vm = DocumentTabViewModel.FromFile("[ERROR] boot at 10.0.0.1", "/var/app.log");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.Contains(editor.TextArea.TextView.LineTransformers, t => t is CodeDecorationColorizer);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_MarkdownTab_HasNoCvColorizer()
    {
        var vm = DocumentTabViewModel.FromFile("# TODO list\n\n2026-01-01", "/docs/readme.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.DoesNotContain(editor.TextArea.TextView.LineTransformers, t => t is CodeDecorationColorizer);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_CvTooltipProbe_ResolvesAUnitUnderThePoint()
    {
        var vm = DocumentTabViewModel.FromFile("вес 5 MB конец", "/var/app.log");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        var textView = editor.TextArea.TextView;
        textView.EnsureVisualLines();

        // Visual mid-point of the "5 MB" token (cols 5..8) → the byte-count tooltip.
        var docPoint = textView.GetVisualPosition(
            new AvaloniaEdit.TextViewPosition(1, 6), AvaloniaEdit.Rendering.VisualYPosition.LineMiddle);
        var tip = view.CvTooltipAt(docPoint - textView.ScrollOffset);

        Assert.Equal("5 242 880 байт", tip);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_CodeTab_AttachesIndentGuides()
    {
        var vm = DocumentTabViewModel.FromFile("if (x)\n    y();", "/src/a.cs");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.Contains(editor.TextArea.TextView.BackgroundRenderers, r => r is IndentGuideRenderer);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_PlainTextTab_HasCvColorizerButNoIndentGuides()
    {
        // .txt is still decorated (dates, urls, units…) but prose must not be striped.
        var vm = DocumentTabViewModel.FromFile("заметка от 01.05.2026", "/notes/a.txt");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        Assert.Contains(editor.TextArea.TextView.LineTransformers, t => t is CodeDecorationColorizer);
        Assert.DoesNotContain(editor.TextArea.TextView.BackgroundRenderers, r => r is IndentGuideRenderer);
        window.Close();
    }

    [AvaloniaFact]
    public void DocumentView_CvTooltipProbe_PlainTextHasNone()
    {
        var vm = DocumentTabViewModel.FromFile("просто текст без токенов", "/var/app.log");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var editor = window.GetVisualDescendants().OfType<TextEditor>().First();
        editor.TextArea.TextView.EnsureVisualLines();

        Assert.Null(view.CvTooltipAt(new Point(8, 8)));
        window.Close();
    }
}
