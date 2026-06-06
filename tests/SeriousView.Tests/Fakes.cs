using System;
using System.Collections.Generic;
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
