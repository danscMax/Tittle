namespace SeriousView.Core.Abstractions;

/// <summary>
/// Shows a native "open file" picker. The implementation (StorageProvider) lives
/// in the UI layer; Core only knows this contract.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Returns the chosen local file paths (the picker is multi-select),
    /// or an empty list if cancelled.</summary>
    Task<IReadOnlyList<string>> PickFilesAsync();

    /// <summary>Shows a save picker and returns the chosen target path, or null if cancelled
    /// (used by the HTML export, M13).</summary>
    Task<string?> SaveFileAsync(string suggestedFileName);
}
