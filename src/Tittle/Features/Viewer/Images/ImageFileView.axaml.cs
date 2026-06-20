using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.VisualTree;
using Tittle.Features.Shell;

namespace Tittle.Features.Viewer.Images;

/// <summary>Image file viewer: fit-to-window or a free/100% zoom (Ctrl+wheel), 90° rotation and a
/// drag-to-pan gesture, over a checkerboard so transparency reads. The image is shown at natural
/// pixels and a LayoutTransform carries rotation × the effective scale; the ScrollViewer pans.</summary>
public partial class ImageFileView : UserControl
{
    private const double Pad = 32;

    private DocumentTabViewModel? _vm;
    private IDisposable? _boundsSub;
    private Point _panStart;
    private Vector _panOffsetStart;
    private bool _panning;

    public ImageFileView()
    {
        InitializeComponent();
        Root.Background = BuildCheckerboard();
        ImageScroll.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        Img.PointerPressed += OnPointerPressed;
        Img.PointerMoved += OnPointerMoved;
        Img.PointerReleased += OnPointerReleased;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _boundsSub = ImageScroll.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(_ => UpdateTransform()));
        UpdateTransform();
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
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as DocumentTabViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmChanged;

        UpdateDimensions();
        UpdateTransform();
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.ImageZoom)
            or nameof(DocumentTabViewModel.ImageFit)
            or nameof(DocumentTabViewModel.ImageRotation))
            UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (_vm?.ImageSource is not { } img || img.Size.Width <= 0 || img.Size.Height <= 0)
            return;

        var scale = _vm.ImageFit ? FitScale() : Math.Clamp(_vm.ImageZoom, 0.05, 16);

        var transform = new TransformGroup();
        transform.Children.Add(new RotateTransform(((_vm.ImageRotation % 360) + 360) % 360));
        transform.Children.Add(new ScaleTransform(scale, scale));
        Xform.LayoutTransform = transform;

        // Fill the viewport so a smaller-than-window image stays centred (the ScrollViewer gives the
        // child infinite height, so without this it would top-align).
        CenterPanel.MinHeight = Math.Max(0, ImageScroll.Bounds.Height - Pad);
    }

    // Largest scale that fits the (rotation-aware) image inside the viewport.
    private double FitScale()
    {
        if (_vm?.ImageSource is not { } img)
            return 1;

        var rotated = (((_vm.ImageRotation % 360) + 360) % 360) is 90 or 270;
        var cw = rotated ? img.Size.Height : img.Size.Width;
        var ch = rotated ? img.Size.Width : img.Size.Height;
        var vw = ImageScroll.Bounds.Width - Pad;
        var vh = ImageScroll.Bounds.Height - Pad;
        return vw > 0 && vh > 0 ? Math.Max(0.01, Math.Min(vw / cw, vh / ch)) : 1;
    }

    private void UpdateDimensions()
    {
        if (_vm?.ImageSource is { } img && img.Size.Width > 0)
            _vm.StatusText = $"Изображение · {(int)Math.Round(img.Size.Width)} × {(int)Math.Round(img.Size.Height)}";
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_vm is null || (e.KeyModifiers & KeyModifiers.Control) == 0)
            return;

        // Leaving fit seeds the free zoom from the current fit scale, so it doesn't jump.
        if (_vm.ImageFit)
        {
            _vm.ImageZoom = FitScale();
            _vm.ImageFit = false;
        }

        var factor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
        _vm.ImageZoom = Math.Clamp(_vm.ImageZoom * factor, 0.05, 16);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Img).Properties.IsLeftButtonPressed)
            return;
        _panStart = e.GetPosition(this);
        _panOffsetStart = ImageScroll.Offset;
        _panning = true;
        e.Pointer.Capture(Img);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning)
            return;
        var delta = e.GetPosition(this) - _panStart;
        ImageScroll.Offset = new Vector(_panOffsetStart.X - delta.X, _panOffsetStart.Y - delta.Y);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panning = false;
        e.Pointer.Capture(null);
    }

    // A subtle tiled checkerboard so transparent regions of an image are visible.
    private static IBrush BuildCheckerboard()
    {
        var geometry = new GeometryGroup();
        geometry.Children.Add(new RectangleGeometry(new Rect(0, 0, 12, 12)));
        geometry.Children.Add(new RectangleGeometry(new Rect(12, 12, 12, 12)));

        return new DrawingBrush
        {
            Drawing = new GeometryDrawing { Brush = new SolidColorBrush(Color.Parse("#16808080")), Geometry = geometry },
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, 24, 24, RelativeUnit.Absolute),
            Stretch = Stretch.None,
        };
    }
}
