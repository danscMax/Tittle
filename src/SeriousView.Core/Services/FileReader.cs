using SeriousView.Core.Abstractions;

namespace SeriousView.Core.Services;

/// <summary>Default <see cref="IFileReader"/> backed by <see cref="File"/>.</summary>
public sealed class FileReader : IFileReader
{
    public bool Exists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => File.ReadAllTextAsync(path, cancellationToken);
}
