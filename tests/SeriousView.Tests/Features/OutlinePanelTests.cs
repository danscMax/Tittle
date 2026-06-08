using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

public class OutlinePanelTests
{
    [AvaloniaFact]
    public void OutlinePanel_ScrollbarAutoHides_LikeTheRestOfTheChrome()
    {
        // The slim auto-hide scrollbar matches the markdown preview's (consistency). The earlier
        // "hover compression" was FluentAvalonia's button template — fixed by the custom outline-item
        // template — not the scrollbar, so the outline no longer pins AllowAutoHide=False.
        var window = new Window { Content = new OutlinePanel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.AllowAutoHide);

        window.Close();
    }
}
