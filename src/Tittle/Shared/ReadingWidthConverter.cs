using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Tittle.Core.Settings;

namespace Tittle.Shared;

/// <summary>
/// Maps the <see cref="ReadingWidth"/> preset onto the preview column: with parameter "align"
/// it yields the <see cref="HorizontalAlignment"/> (Full stretches, the column presets center),
/// otherwise the column's MaxWidth (Full = unbounded). One-way — the radios write the enum.
/// </summary>
public sealed class ReadingWidthConverter : IValueConverter
{
    public static readonly ReadingWidthConverter Instance = new();

    /// <summary>Preset column widths (px).</summary>
    public const double ComfortWidth = 760, NarrowWidth = 620;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var width = value as ReadingWidth? ?? ReadingWidth.Full;
        if (string.Equals(parameter as string, "align", StringComparison.Ordinal))
            return width == ReadingWidth.Full ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

        return width switch
        {
            ReadingWidth.Comfort => ComfortWidth,
            ReadingWidth.Narrow => NarrowWidth,
            _ => double.PositiveInfinity,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
