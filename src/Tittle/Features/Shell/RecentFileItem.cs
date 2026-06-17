using System;
using CommunityToolkit.Mvvm.Input;
using Tittle.Core.Text;

namespace Tittle.Features.Shell;

/// <summary>
/// A recent-file entry shaped for display: the file name and parent folder (via <see cref="RecentFileLabel"/>)
/// plus a self-contained open command. Lets the ☰ File ▸ Recent submenu and the welcome list bind each item
/// directly — no fragile ancestor binding into the window view-model from inside a flyout popup.
/// </summary>
public sealed class RecentFileItem
{
    public string Path { get; }
    public string Name { get; }
    public string Folder { get; }
    public IRelayCommand OpenCommand { get; }

    public RecentFileItem(string path, Action open)
    {
        Path = path;
        (Name, Folder) = RecentFileLabel.Describe(path);
        OpenCommand = new RelayCommand(open);
    }
}
