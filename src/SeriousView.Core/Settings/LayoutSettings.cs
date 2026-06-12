namespace SeriousView.Core.Settings;

/// <summary>Where the application menu lives in the shell chrome.</summary>
public enum MenuPlacement
{
    /// <summary>Hidden behind a ☰ button (the M7.5 default — quietest header).</summary>
    Hidden,

    /// <summary>Classic horizontal menu bar under the title.</summary>
    Bar,

    /// <summary>Compact menu hosted inside the native title bar.</summary>
    TitleBar,
}

/// <summary>How (and whether) the contextual editor toolbar is shown.</summary>
public enum ToolbarMode
{
    /// <summary>No toolbar.</summary>
    Off,

    /// <summary>Thin icon row shown only in Source mode (the default).</summary>
    Contextual,

    /// <summary>Always-on Notepad++-style toolbar.</summary>
    Fixed,
}

/// <summary>Where the Preview/Source view switch is surfaced.</summary>
public enum ViewTogglePlacement
{
    /// <summary>A segmented toggle beside the tabs (the default).</summary>
    Tabs,

    /// <summary>In the status bar.</summary>
    StatusBar,

    /// <summary>Inside the omnibar.</summary>
    Omnibar,
}

/// <summary>Reading-column width preset for the markdown preview (ported reading presets).</summary>
public enum ReadingWidth
{
    /// <summary>Full window width.</summary>
    Full,

    /// <summary>A comfortable centered column (~760 px) — the default: more readable, and resizing
    /// past the cap doesn't re-wrap the document (so it stays smooth, unlike Full).</summary>
    Comfort,

    /// <summary>A narrow book-like column (~620 px).</summary>
    Narrow,
}

/// <summary>Orientation of the split view (source + preview shown together).</summary>
public enum SplitOrientation
{
    /// <summary>Side by side: source left, preview right (the default — wide screens).</summary>
    Horizontal,

    /// <summary>Stacked: source on top, preview below.</summary>
    Vertical,
}

/// <summary>
/// Shell layout / chrome customization. The whole chrome is driven by these knobs rather than being
/// hard-coded, so presets (hamburger, classic menu-bar, in-title-bar) are just different values.
/// Null on <see cref="AppSettings"/> means "all defaults" (the etalon layout). Init-properties (not
/// positional) so new knobs can be added in later milestones without breaking call sites or the
/// persisted JSON shape.
/// </summary>
public sealed record LayoutSettings
{
    /// <summary>Where the menu lives. Default: hidden behind ☰.</summary>
    public MenuPlacement MenuPlacement { get; init; } = MenuPlacement.Hidden;

    /// <summary>Editor toolbar mode. Default: contextual (Source mode only).</summary>
    public ToolbarMode ToolbarMode { get; init; } = ToolbarMode.Contextual;

    /// <summary>Where the Preview/Source toggle sits. Default: beside the tabs.</summary>
    public ViewTogglePlacement ViewTogglePlacement { get; init; } = ViewTogglePlacement.Tabs;

    /// <summary>Show the omnibar (path · 📂 · ⌘). Default: on.</summary>
    public bool ShowOmnibar { get; init; } = true;

    /// <summary>Show the left tool rail. Default: off.</summary>
    public bool ShowRail { get; init; }

    /// <summary>Decorative accent background behind the markdown preview and the outline sidebar.
    /// Default: on. (Field name kept as ReadingMode for the persisted JSON shape.)</summary>
    public bool ReadingMode { get; init; } = true;

    /// <summary>Width of the outline/TOC sidebar in pixels (user-resizable via a splitter).
    /// Default: 240.</summary>
    public double OutlineWidth { get; init; } = 240;

    /// <summary>Reading-column preset for the preview. Default: comfortable centered column.</summary>
    public ReadingWidth ReadingWidth { get; init; } = ReadingWidth.Comfort;

    /// <summary>Orientation of the split view (source + preview together). Default: horizontal.</summary>
    public SplitOrientation SplitOrientation { get; init; } = SplitOrientation.Horizontal;

    /// <summary>Source-pane fraction (0..1) of the split view, user-resizable via a splitter.
    /// Default: 0.5 (even). Clamped to [0.15, 0.85] on use.</summary>
    public double SplitRatio { get; init; } = 0.5;
}
