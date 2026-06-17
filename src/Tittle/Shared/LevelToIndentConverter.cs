using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Tittle.Shared;

/// <summary>Maps a heading level (1–6) to a left-indent <see cref="Thickness"/> so the
/// outline list reflects the document's heading hierarchy.</summary>
public sealed class LevelToIndentConverter : IValueConverter
{
    public static readonly LevelToIndentConverter Instance = new();

    private const double Step = 14;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => new Thickness(value is int level ? (level - 1) * Step : 0, 0, 0, 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
