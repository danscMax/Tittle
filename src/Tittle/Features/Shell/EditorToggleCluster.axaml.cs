using Avalonia.Controls;

namespace Tittle.Features.Shell;

/// <summary>Wrap + line-numbers toggle pair, hosted by both the contextual toolbar and the status-bar
/// fallback in <c>MainWindow</c> (one source instead of two copies).</summary>
public partial class EditorToggleCluster : UserControl
{
    public EditorToggleCluster() => InitializeComponent();
}
