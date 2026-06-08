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

    public LayoutSettings ToSettings() => new()
    {
        MenuPlacement = MenuPlacement,
        ToolbarMode = ToolbarMode,
        ViewTogglePlacement = ViewTogglePlacement,
        ShowOmnibar = ShowOmnibar,
        ShowRail = ShowRail,
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
        };
}
