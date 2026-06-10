using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SeriousView.Core.Settings;

namespace SeriousView.Shared;

/// <summary>
/// Editor display options shared across all open tabs (VS Code-style: one zoom / wrap / line-number
/// state for every source editor). Observable so bound editors update live; mirrored to and from the
/// persisted <see cref="EditorSettings"/>.
/// </summary>
public partial class EditorOptions : ObservableObject
{
    public const double MinFontSize = 8;
    public const double MaxFontSize = 32;
    public const double DefaultFontSize = 14;
    private const double Step = 1;

    [ObservableProperty]
    private double _fontSize = DefaultFontSize;

    [ObservableProperty]
    private bool _wordWrap;

    [ObservableProperty]
    private bool _showLineNumbers = true;

    /// <summary>Default for new tabs: pretty-print .json sources (display-only; ported).</summary>
    [ObservableProperty]
    private bool _jsonPretty;

    /// <summary>Default for new tabs: show .csv/.tsv as a sortable table (ported).</summary>
    [ObservableProperty]
    private bool _csvAsTable = true;

    public void ZoomIn() => FontSize = Math.Min(MaxFontSize, FontSize + Step);
    public void ZoomOut() => FontSize = Math.Max(MinFontSize, FontSize - Step);
    public void ResetZoom() => FontSize = DefaultFontSize;
    public void ToggleWordWrap() => WordWrap = !WordWrap;
    public void ToggleLineNumbers() => ShowLineNumbers = !ShowLineNumbers;

    public EditorSettings ToSettings() => new(FontSize, WordWrap, ShowLineNumbers, JsonPretty, CsvAsTable);

    /// <summary>Build options from persisted settings, clamping the font size defensively.</summary>
    public static EditorOptions FromSettings(EditorSettings? s) => s is null
        ? new EditorOptions()
        : new EditorOptions
        {
            FontSize = Math.Clamp(s.FontSize, MinFontSize, MaxFontSize),
            WordWrap = s.WordWrap,
            ShowLineNumbers = s.ShowLineNumbers,
            JsonPretty = s.JsonPretty,
            CsvAsTable = s.CsvAsTable,
        };
}
