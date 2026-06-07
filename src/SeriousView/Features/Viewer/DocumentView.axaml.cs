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

        // Configure the existing engine (rather than replace it) so the auto-selected
        // theme-aware FluentAvalonia style stays intact:
        //  - harden links — the default command shell-executes any scheme (file://,
        //    custom handlers) from untrusted documents;
        //  - render ::: admonition-* containers (from the Core preprocessor) as callouts.
        if (Preview.Engine is MdEngine engine)
        {
            engine.HyperlinkCommand = SafeHyperlinkCommand.Instance;
            engine.ContainerBlockHandler = new AdmonitionBlockHandler(engine);
        }
    }
}
