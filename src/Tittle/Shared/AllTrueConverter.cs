using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Tittle.Shared;

/// <summary>True only when every bound leg is <c>true</c> — a logical AND for IsVisible MultiBindings
/// (e.g. show the body reading-glow only when a document is open AND the decorative background is on).
/// Defensive about UnsetValue/nulls: any non-true leg reads as false.</summary>
public sealed class AllTrueConverter : IMultiValueConverter
{
    public static readonly AllTrueConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count > 0 && values.All(v => v is true);
}
