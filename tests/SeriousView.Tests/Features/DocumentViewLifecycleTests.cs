using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SeriousView.Features.Shell;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

/// <summary>Guards the subscription-hygiene fix (A4/M5): every child-control/self handler wired in
/// the constructor is detached on final detach, so a closed tab's DocumentView retains no live
/// delegates (kept-alive tabs would otherwise accumulate them across a session).</summary>
public class DocumentViewLifecycleTests
{
    [AvaloniaFact]
    public void Detach_RemovesConstructorWiredHandlers()
    {
        var vm = DocumentTabViewModel.FromFile("# A\n\ntext under it", "/docs/d.md");
        var view = new DocumentView { DataContext = vm };
        var window = new Window { Width = 700, Height = 500, Content = view };

        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.ChildHandlersDetached);

        window.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.ChildHandlersDetached); // the teardown mirror ran on DetachedFromVisualTree
    }
}
