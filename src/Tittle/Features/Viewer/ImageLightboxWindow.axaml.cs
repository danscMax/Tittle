using Avalonia.Controls;
using Avalonia.Media;
using Tittle.Shared;

namespace Tittle.Features.Viewer;

/// <summary>Full-size view of a preview image (ported lightbox): a click dismisses; Esc-close
/// comes from <see cref="ModalWindow"/>.</summary>
public partial class ImageLightboxWindow : ModalWindow
{
    public ImageLightboxWindow()
    {
        InitializeComponent();
        PointerPressed += (_, _) => Close();
    }

    /// <summary>Opens the lightbox over <paramref name="owner"/> at ~90% of its size.</summary>
    public static ImageLightboxWindow Open(Window owner, IImage source)
    {
        var lightbox = new ImageLightboxWindow
        {
            Width = System.Math.Max(320, owner.Bounds.Width * 0.9),
            Height = System.Math.Max(240, owner.Bounds.Height * 0.9),
        };
        lightbox.ImageView.Source = source;
        lightbox.Show(owner);
        return lightbox;
    }
}
