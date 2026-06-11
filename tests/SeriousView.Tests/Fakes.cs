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
    private readonly IReadOnlyDictionary<string, string>? _byPath;

    public FakeFileReader(string content)
        => _result = FileLoadResult.ForText(content, "UTF-8", LineEndings.Detect(content), content.Length);

    public FakeFileReader(FileLoadResult result) => _result = result;

    public FakeFileReader(Exception error) => _error = error;

    /// <summary>Per-path content; any path not in the map throws <see cref="FileNotFoundException"/>
    /// (used to exercise session restore skipping missing files).</summary>
    public FakeFileReader(IReadOnlyDictionary<string, string> byPath) => _byPath = byPath;

    /// <summary>Fail the next N loads with <see cref="FailWith"/> (retry-path testing).</summary>
    public int FailNextLoads { get; set; }

    public Exception FailWith { get; set; } = new IOException("transient share violation");

    public Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (FailNextLoads > 0)
        {
            FailNextLoads--;
            return Task.FromException<FileLoadResult>(FailWith);
        }

        if (_byPath is not null)
            return _byPath.TryGetValue(path, out var content)
                ? Task.FromResult(FileLoadResult.ForText(content, "UTF-8", LineEndings.Detect(content), content.Length))
                : Task.FromException<FileLoadResult>(new FileNotFoundException("missing", path));

        return _error is not null
            ? Task.FromException<FileLoadResult>(_error)
            : Task.FromResult(_result!);
    }
}

internal sealed class FakeFileDialogService(params string?[]? paths) : IFileDialogService
{
    public string? SavePath { get; set; }

    public int SaveCalls { get; private set; }

    public Task<IReadOnlyList<string>> PickFilesAsync() =>
        Task.FromResult<IReadOnlyList<string>>(paths?.OfType<string>().ToList() ?? []);

    public Task<string?> SaveFileAsync(string suggestedFileName)
    {
        SaveCalls++;
        return Task.FromResult(SavePath);
    }
}

internal sealed class FakeDocumentWatcher : IDocumentWatcher
{
    public List<string> Watched { get; } = new();

    public event Action<string, DocumentChangeKind>? Changed;

    public void Watch(string path) => Watched.Add(path);

    public void Unwatch(string path) => Watched.Remove(path);

    /// <summary>Simulate a (debounced) file-system event — synchronous, same thread.</summary>
    public void Raise(string path, DocumentChangeKind kind) => Changed?.Invoke(path, kind);

    public void Dispose() { }
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

    public void ApplyCurrent() { /* no-op: the fake never touches the live Application */ }
}

internal sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public string? LastHtml { get; private set; }

    public string? LastHtmlPlainFallback { get; private set; }

    public Task SetTextAsync(string text)
    {
        LastText = text;
        return Task.CompletedTask;
    }

    public Task SetHtmlAsync(string html, string plainText)
    {
        LastHtml = html;
        LastHtmlPlainFallback = plainText;
        return Task.CompletedTask;
    }
}

internal sealed class FakeShellService : IShellService
{
    public List<string> Revealed { get; } = new();

    public List<string> Opened { get; } = new();

    public void RevealInExplorer(string filePath) => Revealed.Add(filePath);

    public void OpenWithDefaultApp(string filePath) => Opened.Add(filePath);
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

/// <summary>In-memory <see cref="ISettingsStore"/> — keeps values by reference (no JSON round-trip),
/// which is exactly what the holder/VM tests need.</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object?> _values = new();
    public int SaveCount { get; private set; }

    public T? Load<T>(string key) => _values.TryGetValue(key, out var v) ? (T?)v : default;

    public void Save<T>(string key, T value)
    {
        _values[key] = value;
        SaveCount++;
    }
}
