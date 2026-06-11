using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using SeriousView.Features.Shell;

namespace SeriousView.Shared;

/// <summary>
/// TOC item ↔ per-file view state (ported unread marks + bookmarks). Values are
/// [heading ordinal, active tab, view-state version] — the version is bound only to force a
/// recompute on every visited/bookmark mutation. Parameter "unread" yields the unread-dot
/// visibility; "glyph" yields the bookmark glyph (★ when bookmarked, ☆ otherwise).
/// </summary>
public sealed class OutlineViewStateConverter : IMultiValueConverter
{
    public static readonly OutlineViewStateConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not int ordinal
            || values[1] is not DocumentTabViewModel tab)
            return string.Equals(parameter as string, "glyph", StringComparison.Ordinal) ? "☆" : false;

        return (parameter as string) switch
        {
            "glyph" => tab.IsHeadingBookmarked(ordinal) ? "★" : "☆",
            _ => !tab.IsHeadingVisited(ordinal),
        };
    }
}
