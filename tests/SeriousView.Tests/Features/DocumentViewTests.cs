using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
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
    {
        var view = new DocumentView { DataContext = vm };
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
}
