using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
    public void DocumentView_NavigateToHeading_ScrollsWithoutThrowing()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n\ntext\n\n## B", "/docs/readme.md");
        var window = new Window { Content = new DocumentView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Starts in Preview (markdown default) → navigation falls back to source + scroll.
        vm.NavigateToHeadingCommand.Execute(vm.Outline[1]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(DocumentViewMode.Source, vm.ViewMode);
        window.Close();
    }
}
