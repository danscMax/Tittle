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
    public void OutlinePanel_ReservesScrollbarGutter_NoAutoHide()
    {
        // Locks in the reflow fix: with AllowAutoHide the overlay scrollbar would expand on hover
        // and steal width, compressing the rows. Reserving the gutter keeps the layout stable.
        var window = new Window { Content = new OutlinePanel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.False(scroll.AllowAutoHide);

        window.Close();
    }
}
