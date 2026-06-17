using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Tittle.Core.Settings;

namespace Tittle.Shared;

/// <summary>
/// Drives the contextual editor toolbar from <c>[ToolbarMode, ShowSource, HasTabs]</c>.
/// With <c>ConverterParameter="toolbar"</c> the toolbar shows per <see cref="ToolbarMode"/> —
/// Off never, Contextual in Source mode, Fixed whenever a tab is open. With <c>"statusfallback"</c>
/// the wrap/numbers fallback in the status bar shows only when ToolbarMode is Off and the source editor
/// is visible — so those toggles are never duplicated (they live in the toolbar otherwise) nor lost.
/// </summary>
public sealed class ToolbarVisibilityConverter : IMultiValueConverter
{
    public static readonly ToolbarVisibilityConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var mode = values.Count > 0 && values[0] is ToolbarMode m ? m : ToolbarMode.Off;
        var showSource = values.Count > 1 && values[1] is true;
        var hasTabs = values.Count > 2 && values[2] is true;

        if (parameter is "statusfallback")
            return showSource && mode == ToolbarMode.Off;

        return mode switch
        {
            ToolbarMode.Contextual => showSource,
            ToolbarMode.Fixed => hasTabs,
            _ => false, // Off
        };
    }
}
