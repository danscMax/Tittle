namespace SeriousView.Core.Abstractions;

/// <summary>
/// Reads file contents. Abstracted so view models stay testable without touching
/// the real filesystem.
/// </summary>
public interface IFileReader
{
    bool Exists(string path);

    string ReadAllText(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
}
