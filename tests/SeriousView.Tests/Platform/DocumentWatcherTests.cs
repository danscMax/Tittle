using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;
using SeriousView.Platform;
using Xunit;

namespace SeriousView.Tests.Platform;

/// <summary>Integration tests over a real temp directory. Timings are poll-asserted with
/// generous ceilings (FS event latency varies by OS); counts, not timing, are the assertions.</summary>
public class DocumentWatcherTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sv-watch-").FullName;
    private readonly DocumentWatcher _watcher = new(TimeSpan.FromMilliseconds(80));
    private readonly ConcurrentQueue<(string Path, DocumentChangeKind Kind)> _events = new();

    public DocumentWatcherTests() => _watcher.Changed += (p, k) => _events.Enqueue((p, k));

    public void Dispose()
    {
        _watcher.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private string CreateFile(string name, string content = "initial")
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Wait until the queue stops growing (events settled) or the ceiling hits.</summary>
    private async Task<List<(string Path, DocumentChangeKind Kind)>> SettledEventsAsync(int atLeast = 1)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline && _events.Count < atLeast)
            await Task.Delay(25);
        await Task.Delay(250); // grace: catch any straggler that would break a "exactly one" claim
        return _events.ToList();
    }

    [Fact]
    public async Task RapidWrites_CoalesceIntoOneChanged()
    {
        var path = CreateFile("a.md");
        _watcher.Watch(path);

        File.WriteAllText(path, "one");
        File.WriteAllText(path, "two");
        File.WriteAllText(path, "three");

        var events = await SettledEventsAsync();
        var evt = Assert.Single(events);
        Assert.Equal(DocumentChangeKind.Changed, evt.Kind);
        Assert.Equal(path, evt.Path, ignoreCase: true);
    }

    [Fact]
    public async Task TempWritePlusReplace_IsOneChanged_NotARemoval()
    {
        var path = CreateFile("b.md");
        _watcher.Watch(path);

        var temp = Path.Combine(_dir, "b.md.tmp");
        File.WriteAllText(temp, "new content");
        File.Replace(temp, path, destinationBackupFileName: null); // delete+create dance

        var events = await SettledEventsAsync();
        Assert.All(events, e => Assert.Equal(DocumentChangeKind.Changed, e.Kind));
        Assert.Contains(events, e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Delete_RaisesRemoved()
    {
        var path = CreateFile("c.md");
        _watcher.Watch(path);

        File.Delete(path);

        var events = await SettledEventsAsync();
        var evt = Assert.Single(events);
        Assert.Equal(DocumentChangeKind.Removed, evt.Kind);
    }

    [Fact]
    public async Task UnwatchingOneFile_KeepsTheDirectoryWatchForTheOther()
    {
        var keep = CreateFile("keep.md");
        var drop = CreateFile("drop.md");
        _watcher.Watch(keep);
        _watcher.Watch(drop);
        _watcher.Unwatch(drop);

        File.WriteAllText(drop, "ignored");
        File.WriteAllText(keep, "noticed");

        var events = await SettledEventsAsync();
        Assert.NotEmpty(events);
        Assert.All(events, e => Assert.Equal(keep, e.Path, ignoreCase: true));
    }
}
