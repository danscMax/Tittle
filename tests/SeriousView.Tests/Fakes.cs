using System.Threading;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;

namespace SeriousView.Tests;

internal sealed class FakeFileReader(string content, bool exists = true) : IFileReader
{
    public bool Exists(string path) => exists;

    public string ReadAllText(string path) => content;

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(content);
}

internal sealed class FakeFileDialogService(string? path) : IFileDialogService
{
    public Task<string?> PickFileAsync() => Task.FromResult(path);
}

internal sealed class FakeThemeService : IThemeService
{
    public int ToggleCount { get; private set; }

    public void Toggle() => ToggleCount++;
}
