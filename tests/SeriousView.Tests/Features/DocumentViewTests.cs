using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
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
}
