namespace Tittle.Core.Abstractions;

/// <summary>
/// Opens the OS file manager. The implementation (<c>Process.Start</c>) lives in the UI layer; Core only
/// knows this contract. Used by the tab context menu's "reveal in explorer".
/// </summary>
public interface IShellService
{
    /// <summary>Open the OS file manager with <paramref name="filePath"/> selected (where the platform
    /// supports it) or its containing folder. Best-effort — a blank path or an unhandled OS is a no-op.</summary>
    void RevealInExplorer(string filePath);

    /// <summary>Open <paramref name="filePath"/> with its default application (an .html lands in the
    /// browser — the print / save-as-PDF path). Best-effort, never throws.</summary>
    void OpenWithDefaultApp(string filePath);
}
