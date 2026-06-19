using Tittle.Shared;

namespace Tittle.Features.Stats;

/// <summary>Document-statistics popover (ported stats panel); DataContext = TextStats.
/// Esc-close and the Закрыть button both come from <see cref="ModalWindow"/>.</summary>
public partial class StatsWindow : ModalWindow
{
    public StatsWindow()
    {
        InitializeComponent();
    }
}
