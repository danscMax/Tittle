using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SeriousView.Core.Settings;

namespace SeriousView.Shared;

/// <summary>
/// Shell layout options driving the chrome — where the menu lives, the toolbar mode, where the
/// Preview/Source toggle sits, and omnibar/rail visibility. Observable so the chrome re-renders live;
/// mirrored to and from the persisted <see cref="LayoutSettings"/>. One instance per window, mirroring
/// <see cref="EditorOptions"/>. Defaults are the M7.5 etalon (menu hidden behind ☰).
/// </summary>
public partial class LayoutOptions : ObservableObject
{
    [ObservableProperty]
    private MenuPlacement _menuPlacement = MenuPlacement.Hidden;

    [ObservableProperty]
    private ToolbarMode _toolbarMode = ToolbarMode.Contextual;

    [ObservableProperty]
    private ViewTogglePlacement _viewTogglePlacement = ViewTogglePlacement.Tabs;

    [ObservableProperty]
    private bool _showOmnibar = true;

    [ObservableProperty]
    private bool _showRail;

    [ObservableProperty]
    private bool _readingMode = true;

    [ObservableProperty]
    private ReadingWidth _readingWidth = ReadingWidth.Comfort;

    [ObservableProperty]
    private SplitOrientation _splitOrientation = SplitOrientation.Horizontal;

    /// <summary>Source-pane fraction of the split view. Shared by the splitter (view) and persistence.</summary>
    public const double MinSplitRatio = 0.15, MaxSplitRatio = 0.85, DefaultSplitRatio = 0.5;

    /// <summary>Clamp a split ratio into the allowed range (NaN → default).</summary>
    public static double ClampSplitRatio(double r) =>
        Math.Clamp(double.IsNaN(r) ? DefaultSplitRatio : r, MinSplitRatio, MaxSplitRatio);

    [ObservableProperty]
    private double _splitRatio = DefaultSplitRatio;

    // Keep the ratio sane even if a settings file is hand-edited out of range.
    partial void OnSplitRatioChanged(double value)
    {
        var clamped = ClampSplitRatio(value);
        if (clamped != value)
            SplitRatio = clamped; // re-set lands in range; an equal value is a no-op (no loop)
    }

    /// <summary>Outline/TOC sidebar width range (px). Shared by the splitter (view) and persistence.</summary>
    public const double MinOutlineWidth = 180, MaxOutlineWidth = 480, DefaultOutlineWidth = 240;

    /// <summary>Clamp a width into the allowed range (NaN → default).</summary>
    public static double ClampOutlineWidth(double w) =>
        Math.Clamp(double.IsNaN(w) ? DefaultOutlineWidth : w, MinOutlineWidth, MaxOutlineWidth);

    [ObservableProperty]
    private double _outlineWidth = DefaultOutlineWidth;

    // Keep the persisted/observable width sane even if a settings file is hand-edited out of range.
    partial void OnOutlineWidthChanged(double value)
    {
        var clamped = ClampOutlineWidth(value);
        if (clamped != value)
            OutlineWidth = clamped; // re-set lands in range; an equal value is a no-op (no loop)
    }

    public LayoutSettings ToSettings() => new()
    {
        MenuPlacement = MenuPlacement,
        ToolbarMode = ToolbarMode,
        ViewTogglePlacement = ViewTogglePlacement,
        ShowOmnibar = ShowOmnibar,
        ShowRail = ShowRail,
        ReadingMode = ReadingMode,
        OutlineWidth = OutlineWidth,
        ReadingWidth = ReadingWidth,
        SplitOrientation = SplitOrientation,
        SplitRatio = SplitRatio,
    };

    /// <summary>Build options from persisted settings, or the etalon defaults when none are saved.</summary>
    public static LayoutOptions FromSettings(LayoutSettings? s) => s is null
        ? new LayoutOptions()
        : new LayoutOptions
        {
            MenuPlacement = s.MenuPlacement,
            ToolbarMode = s.ToolbarMode,
            ViewTogglePlacement = s.ViewTogglePlacement,
            ShowOmnibar = s.ShowOmnibar,
            ShowRail = s.ShowRail,
            ReadingMode = s.ReadingMode,
            OutlineWidth = s.OutlineWidth,
            ReadingWidth = s.ReadingWidth,
            SplitOrientation = s.SplitOrientation,
            SplitRatio = s.SplitRatio,
        };
}
