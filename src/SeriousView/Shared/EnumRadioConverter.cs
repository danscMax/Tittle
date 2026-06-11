using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SeriousView.Shared;

/// <summary>
/// Two-way enum ↔ bool for RadioButton groups in the settings panel: <see cref="Convert"/> is true when
/// the bound enum equals the <c>ConverterParameter</c> member name; <see cref="ConvertBack"/> returns that
/// enum member when the radio is turned ON and <see cref="BindingOperations.DoNothing"/> when it's turned
/// OFF, so only the newly-checked radio writes back. (<c>EnumToBoolConverter</c> is one-way — its
/// ConvertBack throws — hence this variant.)
/// </summary>
public sealed class EnumRadioConverter : IValueConverter
{
    public static readonly EnumRadioConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => EnumToBoolConverter.NameMatches(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true && parameter is not null && targetType.IsEnum
            ? Enum.Parse(targetType, parameter.ToString()!)
            : BindingOperations.DoNothing; // the radio turning OFF must not write back
}
