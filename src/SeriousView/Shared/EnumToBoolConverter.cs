using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SeriousView.Shared;

/// <summary>
/// True when the bound value's enum member name equals the <c>ConverterParameter</c> string.
/// Lets XAML react to an enum without a converter per case — drives the ☰ visibility
/// (<c>MenuPlacement == Hidden</c>) and the View ▸ Theme radio check-marks (<c>ThemeMode == …</c>).
/// </summary>
public sealed class EnumToBoolConverter : IValueConverter
{
    public static readonly EnumToBoolConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null
           && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
