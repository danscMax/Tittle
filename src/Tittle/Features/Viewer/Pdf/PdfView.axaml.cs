using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using Tittle.Features.Shell;
using Tittle.Shared;

namespace Tittle.Features.Viewer.Pdf;

/// <summary>Drives the PDF page list: lazily renders a page when virtualization realizes its
/// container (releases it on clearing), re-fits the page width to the viewport (or natural «100%»
/// width) × the shared reading zoom, reports the current page «N / M» from the scroll position, and
/// scrolls to a requested page (Ctrl+G).</summary>
public partial class PdfView : UserControl
{
    private const double PageGap = 16; // bottom margin per page (matches the XAML), also the top padding

    private DocumentTabViewModel? _vm;
    private EditorOptions? _editor;
    private IDisposable? _boundsSub;

    public PdfView()
    {
        InitializeComponent();
        PagesList.ContainerPrepared += OnContainerPrepared;
        PagesList.ContainerClearing += OnContainerClearing;
        PageScroll.ScrollChanged += (_, _) => UpdatePageText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _boundsSub = PageScroll.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(_ => UpdatePageWidths()));
        UpdatePageWidths();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _boundsSub?.Dispose();
        _boundsSub = null;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_editor is not null)
            _editor.PropertyChanged -= OnEditorPropertyChanged;
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PdfGoToPageRequested -= OnGoToPage;
        }

        _vm = DataContext as DocumentTabViewModel;
        _editor = _vm?.Editor;
        if (_editor is not null)
            _editor.PropertyChanged += OnEditorPropertyChanged;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.PdfGoToPageRequested += OnGoToPage;
        }

        UpdatePageWidths();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorOptions.FontSize) or nameof(EditorOptions.PreviewScale))
            UpdatePageWidths();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTabViewModel.PdfActualSize))
            UpdatePageWidths();
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is PdfPageViewModel page)
            page.EnsureRendered((int)Math.Ceiling(page.DisplayWidth));
    }

    private void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        if (e.Container.DataContext is PdfPageViewModel page)
            page.Release();
    }

    // Fit-to-viewport width (default) or the page's natural «100%» width, each × the shared reading
    // zoom. Re-renders the realized pages so a resize / zoom / mode-flip isn't blurry.
    private void UpdatePageWidths()
    {
        if (_vm?.Pdf is not { } pdf)
            return;

        var available = PageScroll.Bounds.Width;
        if (available <= 1)
            return;

        var scale = _vm.Editor?.PreviewScale ?? 1.0;
        var actual = _vm.PdfActualSize;

        foreach (var page in pdf.Pages)
            page.DisplayWidth = Math.Clamp((actual ? page.NaturalWidth : available - 36) * scale, 80, 6000);

        foreach (var container in PagesList.GetRealizedContainers())
            if (container.DataContext is PdfPageViewModel p)
                p.EnsureRendered((int)Math.Ceiling(p.DisplayWidth));

        UpdatePageText();
    }

    // The page occupying the top of the viewport → "стр N / M" in the status bar.
    private void UpdatePageText()
    {
        if (_vm?.Pdf is not { } pdf || pdf.PageCount == 0)
            return;

        var offset = PageScroll.Offset.Y;
        var y = PageGap; // top padding
        var current = 1;
        for (var i = 0; i < pdf.Pages.Count; i++)
        {
            current = i + 1;
            var blockBottom = y + pdf.Pages[i].DisplayHeight + PageGap;
            if (offset + 1 < blockBottom)
                break;
            y = blockBottom;
        }

        _vm.PdfPageText = $"стр {current} / {pdf.PageCount}";
    }

    // Scroll a 1-based page to (near) the top of the viewport.
    private void OnGoToPage(int page)
    {
        if (_vm?.Pdf is not { } pdf)
            return;

        var y = PageGap;
        for (var i = 0; i < page - 1 && i < pdf.Pages.Count; i++)
            y += pdf.Pages[i].DisplayHeight + PageGap;

        // Scroll exactly to the page top so the counter (UpdatePageText, same accumulation) agrees.
        PageScroll.Offset = PageScroll.Offset.WithY(Math.Max(0, y));
    }
}
