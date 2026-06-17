using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace Tittle.Features.Palette;

/// <summary>One entry in the command palette: a human title, the command it runs, an optional shortcut
/// hint, and (set during filtering) the matched character indices for highlight.</summary>
public sealed class PaletteItem
{
    public PaletteItem(string title, ICommand command, string? shortcut = null, object? parameter = null)
    {
        Title = title;
        Command = command;
        Shortcut = shortcut;
        Parameter = parameter;
    }

    public string Title { get; }
    public ICommand Command { get; }
    public string? Shortcut { get; }
    public object? Parameter { get; }

    /// <summary>Matched character indices from the last filter pass (for highlight); empty when unfiltered.</summary>
    public IReadOnlyList<int> Indices { get; set; } = Array.Empty<int>();

    public bool CanRun => Command.CanExecute(Parameter);
    public void Run() => Command.Execute(Parameter);
}
