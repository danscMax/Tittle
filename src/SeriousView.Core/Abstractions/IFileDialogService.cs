namespace SeriousView.Core.Abstractions;

/// <summary>
/// Shows a native "open file" picker. The implementation (StorageProvider) lives
/// in the UI layer; Core only knows this contract.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Returns the chosen local file path, or <c>null</c> if cancelled.</summary>
    Task<string?> PickFileAsync();
}
