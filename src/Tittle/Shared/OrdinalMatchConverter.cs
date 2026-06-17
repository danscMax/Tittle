using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tittle.Shared;

/// <summary>True when two bound ordinals match — marks the outline item whose heading is the
/// active one (M10). Defensive about UnsetValue/nulls: a null <c>SelectedTab</c> leg must read
/// as "no match", never as visible (the null-path compiled-binding default trap).</summary>
public sealed class OrdinalMatchConverter : IMultiValueConverter
{
    public static readonly OrdinalMatchConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count == 2 && values[0] is int left && values[1] is int right && left == right;
}
