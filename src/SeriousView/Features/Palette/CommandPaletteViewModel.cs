using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SeriousView.Core.Commands;
using SeriousView.Shared;

namespace SeriousView.Features.Palette;

/// <summary>Drives the Ctrl+K command palette: fuzzy-filters a fixed command list by the query,
/// ranks by score, tracks the keyboard selection, and runs the chosen command. UI-free / headless-testable.</summary>
public partial class CommandPaletteViewModel : ViewModelBase
{
    private readonly IReadOnlyList<PaletteItem> _all;

    public CommandPaletteViewModel(IReadOnlyList<PaletteItem> items)
    {
        _all = items;
        _results = Filter("");
        _selectedIndex = _results.Count > 0 ? 0 : -1;
    }

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    private IReadOnlyList<PaletteItem> _results;

    [ObservableProperty]
    private int _selectedIndex;

    public bool HasResults => Results.Count > 0;

    /// <summary>Raised when the palette should close (a command ran, or the user cancelled).</summary>
    public event Action? Closed;

    partial void OnQueryChanged(string value)
    {
        Results = Filter(value);
        SelectedIndex = Results.Count > 0 ? 0 : -1;
    }

    private IReadOnlyList<PaletteItem> Filter(string query)
    {
        var scored = new List<(PaletteItem Item, int Score, int Order)>();
        for (var i = 0; i < _all.Count; i++)
        {
            if (FuzzyMatcher.Match(query, _all[i].Title) is { } m)
            {
                _all[i].Indices = m.Indices; // for the highlight in the item template
                scored.Add((_all[i], m.Score, i));
            }
        }
        return scored.OrderByDescending(s => s.Score).ThenBy(s => s.Order).Select(s => s.Item).ToList();
    }

    /// <summary>Move the selection by <paramref name="delta"/>, wrapping (↑/↓).</summary>
    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
            return;
        SelectedIndex = (SelectedIndex + delta + Results.Count) % Results.Count;
    }

    /// <summary>Run the selected command (if runnable) and close.</summary>
    public void Execute()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Results.Count && Results[SelectedIndex].CanRun)
            Results[SelectedIndex].Run();
        Close();
    }

    public void Close() => Closed?.Invoke();
}
