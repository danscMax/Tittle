using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tittle.Features.Palette;

/// <summary>A contiguous slice of a palette title, flagged as matched (highlight) or not.</summary>
public sealed record TextRun(string Text, bool IsMatch);

/// <summary>Splits a <see cref="PaletteItem"/>'s title into matched / unmatched runs (using its
/// <see cref="PaletteItem.Indices"/>) so the item template can highlight the fuzzy-matched characters.</summary>
public sealed class MatchHighlightConverter : IValueConverter
{
    public static readonly MatchHighlightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PaletteItem item)
            return Array.Empty<TextRun>();

        var title = item.Title;
        var hit = new bool[title.Length];
        foreach (var i in item.Indices)
            if (i >= 0 && i < hit.Length)
                hit[i] = true;

        var runs = new List<TextRun>();
        var start = 0;
        for (var i = 1; i <= title.Length; i++)
        {
            if (i == title.Length || hit[i] != hit[start])
            {
                runs.Add(new TextRun(title[start..i], hit[start]));
                start = i;
            }
        }
        return runs;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
