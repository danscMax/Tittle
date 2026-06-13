using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using SeriousView.Features.Shell;
using SeriousView.Shared;

namespace SeriousView.Features.Viewer.Pdf;

/// <summary>Drives the PDF page list: lazily renders a page when virtualization realizes its
/// container (and releases it on clearing), and re-fits the page width to the viewport × the shared
/// reading zoom on resize / zoom change. Overlays over the AvaloniaEdit GPU surface never repaint,
/// but this is a plain control tree, so a normal ScrollViewer + ItemsControl is fine here.</summary>
public partial class PdfView : UserControl
{
    private DocumentTabViewModel? _vm;
    private EditorOptions? _editor;
    private IDisposable? _boundsSub;

    public PdfView()
    {
        InitializeComponent();
        PagesList.ContainerPrepared += OnContainerPrepared;
        PagesList.ContainerClearing += OnContainerClearing;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _boundsSub = PageScroll.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver(UpdatePageWidths));
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

        _vm = DataContext as DocumentTabViewModel;
        _editor = _vm?.Editor;
        if (_editor is not null)
            _editor.PropertyChanged += OnEditorPropertyChanged;

        UpdatePageWidths();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorOptions.FontSize) or nameof(EditorOptions.PreviewScale))
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

    // Fit-to-viewport width × the shared reading zoom (the same Editor.FontSize that scales the
    // markdown preview). Re-renders the realized pages so a resize / zoom isn't blurry.
    private void UpdatePageWidths()
    {
        if (_vm?.Pdf is not { } pdf)
            return;

        var available = PageScroll.Bounds.Width;
        if (available <= 1)
            return;

        var scale = _vm.Editor?.PreviewScale ?? 1.0;
        var width = Math.Clamp((available - 36) * scale, 80, 3000);

        foreach (var page in pdf.Pages)
            page.DisplayWidth = width;

        foreach (var container in PagesList.GetRealizedContainers())
            if (container.DataContext is PdfPageViewModel page)
                page.EnsureRendered((int)Math.Ceiling(page.DisplayWidth));
    }

    // Minimal IObserver for the Bounds observable — fires the resize re-fit (no Rx dependency).
    private sealed class AnonymousObserver(Action onNext) : IObserver<Rect>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(Rect value) => onNext();
    }
}
