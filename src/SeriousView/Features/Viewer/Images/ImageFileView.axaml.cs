using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SeriousView.Features.Viewer.Images;

/// <summary>Image file viewer: keeps the image fitted to the viewport (Stretch=Uniform bounded by
/// the live viewport size) so the shared reading zoom (a LayoutTransform in the XAML) scales up
/// from a fit baseline, with the ScrollViewer panning when zoomed past the viewport.</summary>
public partial class ImageFileView : UserControl
{
    private const double Pad = 36;
    private IDisposable? _boundsSub;

    public ImageFileView() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _boundsSub = ImageScroll.GetObservable(BoundsProperty).Subscribe(new BoundsObserver(FitToViewport));
        FitToViewport();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _boundsSub?.Dispose();
        _boundsSub = null;
    }

    // Bound the logical image to the viewport so Uniform fits it at zoom 1.0; the LayoutTransform
    // then scales from this fit baseline.
    private void FitToViewport()
    {
        var b = ImageScroll.Bounds;
        Img.MaxWidth = Math.Max(1, b.Width - Pad);
        Img.MaxHeight = Math.Max(1, b.Height - Pad);
    }

    private sealed class BoundsObserver(Action onNext) : IObserver<Rect>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(Rect value) => onNext();
    }
}
