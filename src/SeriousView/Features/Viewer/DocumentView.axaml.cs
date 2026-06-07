using Avalonia.Controls;
using MdEngine = Markdown.Avalonia.Markdown;

namespace SeriousView.Features.Viewer;

/// <summary>Per-tab body: switches between the source editor and the rendered
/// markdown preview (driven by the tab VM's ShowSource/ShowPreview).</summary>
public partial class DocumentView : UserControl
{
    public DocumentView()
    {
        InitializeComponent();

        // Harden link handling: Markdown.Avalonia's default command shell-executes any
        // scheme (file://, custom handlers) from untrusted documents. We mutate the
        // existing engine (rather than replace it) so the auto-selected theme-aware
        // FluentAvalonia style stays intact.
        if (Preview.Engine is MdEngine engine)
            engine.HyperlinkCommand = SafeHyperlinkCommand.Instance;
    }
}
