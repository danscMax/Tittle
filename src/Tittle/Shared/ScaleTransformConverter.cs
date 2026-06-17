using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Tittle.Shared;

/// <summary>
/// Maps a scalar zoom factor onto a uniform <see cref="ScaleTransform"/> for a
/// <c>LayoutTransformControl.LayoutTransform</c>. Used to zoom the markdown preview by the shared
/// editor font scale (<see cref="EditorOptions.PreviewScale"/>). Binding the transform via a
/// converter on the control's property (rather than binding the transform's ScaleX/ScaleY directly)
/// avoids the well-known gotcha that a DataContext does not flow into a <see cref="Transform"/>,
/// which lives outside the visual/logical tree.
/// </summary>
public sealed class ScaleTransformConverter : IValueConverter
{
    public static readonly ScaleTransformConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var scale = value is double d && d > 0 ? d : 1.0;
        return new ScaleTransform(scale, scale);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
