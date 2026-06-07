using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Text;

namespace SeriousView.Tests;

internal sealed class FakeFileReader : IFileReader
{
    private readonly FileLoadResult? _result;
    private readonly Exception? _error;

    public FakeFileReader(string content)
        => _result = FileLoadResult.ForText(content, "UTF-8", LineEndings.Detect(content), content.Length);

    public FakeFileReader(FileLoadResult result) => _result = result;

    public FakeFileReader(Exception error) => _error = error;

    public Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
        => _error is not null
            ? Task.FromException<FileLoadResult>(_error)
            : Task.FromResult(_result!);
}

internal sealed class FakeFileDialogService(string? path) : IFileDialogService
{
    public Task<string?> PickFileAsync() => Task.FromResult(path);
}

internal sealed class FakeThemeService : IThemeService
{
    public ThemeMode Mode { get; private set; } = ThemeMode.Dark;
    public int ChangeCount { get; private set; }
    public event EventHandler? Changed;

    public void SetMode(ThemeMode mode)
    {
        Mode = mode;
        ChangeCount++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cycle() => SetMode(Mode.Next());
}

internal sealed class FakeRecentFilesStore : IRecentFilesStore
{
    private readonly List<string> _items = new();
    public IReadOnlyList<string> Items => _items;
    public event EventHandler? Changed;

    public void Add(string path)
    {
        _items.Remove(path);
        _items.Insert(0, path);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
