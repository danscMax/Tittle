using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        // Relay the editor caret position to the active tab VM (shown in the status bar).
        Source.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Unsubscribe();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unsubscribe();
        _vm = DataContext as DocumentTabViewModel;
        if (_vm is not null)
        {
            _vm.NavigationRequested += OnNavigationRequested;
            _vm.GoToLineRequested += OnGoToLineRequested;
            _vm.PropertyChanged += OnVmPropertyChanged;
            // After the new document/layout settles: refresh the caret readout and, for a source
            // tab, focus the editor so the keyboard works immediately (#29).
            Dispatcher.UIThread.Post(ActivateSource);
        }
    }

    // Tabs are kept alive (DataContext is set once), so focus must follow ACTIVATION: re-focus the
    // editor when this tab becomes the active one.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTabViewModel.IsActive) && _vm?.IsActive == true)
            Dispatcher.UIThread.Post(ActivateSource);
    }

    // The go-to-line request is raised by the status-bar input (wired in MainWindow); scroll there.
    private void OnGoToLineRequested(int line) => Dispatcher.UIThread.Post(() =>
    {
        ScrollSourceToLine(line);
        Source.TextArea.Focus();
    });

    private void OnCaretPositionChanged(object? sender, EventArgs e) => UpdateCaret();

    private void UpdateCaret()
    {
        if (_vm is null)
            return;

        var caret = Source.TextArea.Caret;
        _vm.CaretLine = caret.Line;
        _vm.CaretColumn = caret.Column;
    }

    private void ActivateSource()
    {
        // Only the active (visible) tab may take focus — never steal it to a hidden, kept-alive view.
        if (_vm is null || !_vm.IsActive)
            return;

        UpdateCaret();
        if (_vm.ShowSource)
            Source.TextArea.Focus(); // the TextArea handles keyboard, not the TextEditor wrapper
    }

    private void Unsubscribe()
    {
        if (_vm is not null)
        {
            _vm.NavigationRequested -= OnNavigationRequested;
            _vm.GoToLineRequested -= OnGoToLineRequested;
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = null;
    }

    private void OnNavigationRequested(HeadingOutline heading)
    {
        if (_vm is null)
            return;

        // In preview, scroll the rendered document in place; if the heading control can't be
        // located, fall back to the reliable line-based source scroll (switching mode first).
        if (_vm.ShowPreview)
        {
            if (TryScrollPreviewToHeading(heading.Ordinal))
                return;
            _vm.ViewMode = DocumentViewMode.Source;
        }

        // Defer so the editor is laid out (and visible after a mode switch) before scrolling.
        Dispatcher.UIThread.Post(() => ScrollSourceToLine(heading.Line));
    }

    /// <summary>Scroll the preview to the <paramref name="ordinal"/>-th heading by walking the
    /// visual tree (Markdown.Avalonia exposes no scroll API). Returns false if not found.</summary>
    private bool TryScrollPreviewToHeading(int ordinal)
    {
        var headings = Preview.GetVisualDescendants().OfType<Control>().Where(IsTopLevelHeading).ToList();
        if (ordinal < 0 || ordinal >= headings.Count)
            return false;

        headings[ordinal].BringIntoView();
        return true;
    }

    // Markdown.Avalonia renders headings as controls with a "Heading1".."Heading6" style class.
    // Headings inside an admonition callout are excluded so the order matches the Core outline
    // (which skips blockquoted headings). NB: depends on that class naming — recheck on upgrade.
    private static bool IsTopLevelHeading(Control control)
        => control.Classes.Any(c => c is "Heading1" or "Heading2" or "Heading3"
                                      or "Heading4" or "Heading5" or "Heading6")
        && !control.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("admonition"));

    private void ScrollSourceToLine(int line1Based)
    {
        var caret = Source.TextArea.Caret;
        caret.Line = line1Based;
        caret.Column = 1;
        caret.BringCaretToView();
    }
}
