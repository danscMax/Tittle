using System;
using Avalonia.Controls;
using Avalonia.Threading;
using SeriousView.Core.Text;
using SeriousView.Features.Shell;
using MdEngine = Markdown.Avalonia.Markdown;

namespace SeriousView.Features.Viewer;

/// <summary>Per-tab body: switches between the source editor and the rendered
/// markdown preview (driven by the tab VM's ShowSource/ShowPreview), and scrolls
/// to a heading when the outline raises a navigation request.</summary>
public partial class DocumentView : UserControl
{
    private DocumentTabViewModel? _vm;

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

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _vm = DataContext as DocumentTabViewModel;
        if (_vm is not null)
            _vm.NavigationRequested += OnNavigationRequested;
    }

    private void Unsubscribe()
    {
        if (_vm is not null)
            _vm.NavigationRequested -= OnNavigationRequested;
        _vm = null;
    }

    private void OnNavigationRequested(HeadingOutline heading)
    {
        // Source-mode navigation is reliable (line-based). In preview mode we currently
        // fall back to source; C4 replaces this with in-place preview scrolling.
        if (_vm is { ShowPreview: true })
            _vm.ViewMode = DocumentViewMode.Source;

        // Defer so the editor is laid out (and visible after a mode switch) before scrolling.
        Dispatcher.UIThread.Post(() => ScrollSourceToLine(heading.Line));
    }

    private void ScrollSourceToLine(int line1Based)
    {
        var caret = Source.TextArea.Caret;
        caret.Line = line1Based;
        caret.Column = 1;
        caret.BringCaretToView();
    }
}
