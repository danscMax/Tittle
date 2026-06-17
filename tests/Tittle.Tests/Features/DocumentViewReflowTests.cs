using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tittle.Features.Shell;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

/// <summary>Guards the resize-storm fix (A2): the heavy preview-reflow passes (heading-Y rebuild,
/// embedded code-editor height pinning, sorter/collapser attach) must NOT run once per extent-change
/// frame. The first layout primes them immediately; every later reflow coalesces onto a debounce.</summary>
public class DocumentViewReflowTests
{
    // Image-free markdown with headings, a table and a code fence — exercises every heavy pass.
    private const string Sample = """
        # First

        Text under the first heading with **bold**.

        | A | B |
        |---|---|
        | 1 | 2 |

        ## Second

        ```cs
        var x = 1;
        ```
        """;

    [AvaloniaFact]
    public void Preview_FirstLayout_PrimesReflowImmediately()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 800, Height = 600, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // The opened document is complete in one frame — the first reflow ran synchronously.
        Assert.True(view.PreviewReflowPassCount >= 1);

        window.Close();
    }

    [AvaloniaFact]
    public void Preview_ResizeStorm_CoalescesReflow_NotPerFrame()
    {
        var vm = DocumentTabViewModel.FromFile(Sample, "/docs/readme.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 800, Height = 600, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var primed = view.PreviewReflowPassCount; // first layout's immediate pass
        Assert.True(primed >= 1);

        // A burst of extent changes (a resize drag) must not each run the heavy pass — they all
        // restart the debounce timer, which has not fired (the headless clock isn't advanced).
        for (var i = 0; i < 12; i++)
            view.SimulatePreviewExtentChangeForTest();

        Assert.Equal(primed, view.PreviewReflowPassCount); // zero extra synchronous passes
        Assert.True(view.PreviewReflowPending);            // all coalesced into one pending run

        window.Close();
    }
}
