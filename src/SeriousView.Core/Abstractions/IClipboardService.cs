namespace SeriousView.Core.Abstractions;

/// <summary>
/// Writes text to the system clipboard. The implementation (Avalonia's <c>TopLevel.Clipboard</c>) lives
/// in the UI layer; Core only knows this contract. Used by the tab context menu's copy-path / copy-name.
/// </summary>
public interface IClipboardService
{
    /// <summary>Put <paramref name="text"/> on the clipboard. A no-op when no clipboard is available.</summary>
    Task SetTextAsync(string text);
}
