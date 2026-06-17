using System;
using System.Threading;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tittle.Features.Viewer.Pdf;

/// <summary>One PDF page: a sized placeholder that lazily renders its bitmap when the view
/// realizes it (virtualization), and releases it when scrolled away. Width is driven by the view
/// (fit-to-viewport × zoom); height follows the page aspect ratio so the placeholder is correctly
/// sized before — and after — its bitmap loads.</summary>
public sealed partial class PdfPageViewModel : ObservableObject
{
    private const double DefaultWidth = 600;

    private readonly PdfDocumentSource _source;

    public int Index { get; }

    /// <summary>Page height / width.</summary>
    public double AspectRatio { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHeight))]
    private double _displayWidth = DefaultWidth;

    public double DisplayHeight => DisplayWidth * AspectRatio;

    [ObservableProperty]
    private Bitmap? _image;

    private CancellationTokenSource? _cts;
    private int _renderedWidth = -1;

    public PdfPageViewModel(PdfDocumentSource source, int index)
    {
        _source = source;
        Index = index;
        AspectRatio = index < source.AspectRatios.Count ? source.AspectRatios[index] : 1.4142;
        NaturalWidth = index < source.NaturalWidths.Count ? source.NaturalWidths[index] : 816;
    }

    /// <summary>Natural page width in CSS pixels — the target for the «100%» view.</summary>
    public double NaturalWidth { get; }

    /// <summary>Render (or re-render at a new width) this page. Fire-and-forget from the view's
    /// container-realization / resize hooks; a superseding call cancels the in-flight render.</summary>
    public async void EnsureRendered(int pixelWidth)
    {
        if (pixelWidth <= 0 || pixelWidth == _renderedWidth)
            return;

        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        try
        {
            var bmp = await _source.RenderPageAsync(Index, pixelWidth, cts.Token);
            if (bmp is not null && !cts.IsCancellationRequested)
            {
                _renderedWidth = pixelWidth;
                Image = bmp;
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer width — keep the existing bitmap/placeholder
        }
        catch
        {
            // a single bad page leaves its placeholder; the rest of the document still renders
        }
    }

    /// <summary>Drop the rendered bitmap when the page scrolls out of view (frees memory).</summary>
    public void Release()
    {
        _cts?.Cancel();
        Image = null;
        _renderedWidth = -1;
    }
}
