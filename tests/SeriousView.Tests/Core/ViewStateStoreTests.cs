using System.Collections.Generic;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;
using SeriousView.Core.Settings;
using Xunit;

namespace SeriousView.Tests.Core;

public class ViewStateStoreTests
{
    private sealed class FakeStore : ISettingsStore
    {
        public Dictionary<string, object?> Data { get; } = new();
        public int Saves;

        public T? Load<T>(string key) => Data.TryGetValue(key, out var v) ? (T?)v : default;

        public void Save<T>(string key, T value)
        {
            Data[key] = value;
            Saves++;
        }
    }

    [Fact]
    public void MarkVisited_AccumulatesPerFile()
    {
        var store = new ViewStateStore(new FakeStore());

        store.MarkVisited("/docs/a.md", 0);
        store.MarkVisited("/docs/a.md", 2);
        store.MarkVisited("/docs/b.md", 1);

        Assert.True(store.IsVisited("/docs/a.md", 0));
        Assert.True(store.IsVisited("/docs/a.md", 2));
        Assert.False(store.IsVisited("/docs/a.md", 1));
        Assert.True(store.IsVisited("/docs/b.md", 1));
    }

    [Fact]
    public void ToggleBookmark_FlipsAndReports()
    {
        var store = new ViewStateStore(new FakeStore());

        Assert.True(store.ToggleBookmark("/docs/a.md", 3));
        Assert.True(store.IsBookmarked("/docs/a.md", 3));
        Assert.False(store.ToggleBookmark("/docs/a.md", 3));
        Assert.False(store.IsBookmarked("/docs/a.md", 3));
    }

    [Fact]
    public void Paths_AreCaseInsensitiveLikeTheTabReuseRule()
    {
        var store = new ViewStateStore(new FakeStore());

        store.MarkVisited(@"C:\Docs\A.md", 0);

        Assert.True(store.IsVisited(@"c:\docs\a.md", 0));
    }

    [Fact]
    public void Flush_PersistsAndReloads()
    {
        var fake = new FakeStore();
        var store = new ViewStateStore(fake);
        store.MarkVisited("/docs/a.md", 0);
        store.ToggleBookmark("/docs/a.md", 1);
        store.Flush();

        var reloaded = new ViewStateStore(fake);

        Assert.True(reloaded.IsVisited("/docs/a.md", 0));
        Assert.True(reloaded.IsBookmarked("/docs/a.md", 1));
    }

    [Fact]
    public void Flush_WithoutChanges_DoesNotWrite()
    {
        var fake = new FakeStore();
        var store = new ViewStateStore(fake);

        store.Flush();

        Assert.Equal(0, fake.Saves);
    }

    [Fact]
    public void Flush_PrunesToTheFileCap_DroppingTheLeastRecentlyTouched()
    {
        var fake = new FakeStore();
        var store = new ViewStateStore(fake);
        for (var i = 0; i <= ViewStateStore.MaxFiles; i++)
            store.MarkVisited($"/docs/f{i}.md", 0);
        store.Flush();

        var reloaded = new ViewStateStore(fake);

        Assert.False(reloaded.IsVisited("/docs/f0.md", 0));                      // the oldest fell off
        Assert.True(reloaded.IsVisited($"/docs/f{ViewStateStore.MaxFiles}.md", 0)); // the newest stays
    }

    [Fact]
    public void Flush_PrunesTheInMemoryMap_NotJustTheSerializedSnapshot()
    {
        // Audit V11: a long session that opens many files must not keep every entry live in RAM.
        var store = new ViewStateStore(new FakeStore());
        for (var i = 0; i < ViewStateStore.MaxFiles + 50; i++)
            store.MarkVisited($"/docs/f{i}.md", 0);

        Assert.Equal(ViewStateStore.MaxFiles + 50, store.TrackedCount); // all held before Flush
        store.Flush();
        Assert.Equal(ViewStateStore.MaxFiles, store.TrackedCount);      // trimmed to the cap in memory
    }

    [Fact]
    public void BookmarksFor_ReturnsOrdinalsInOrder()
    {
        var store = new ViewStateStore(new FakeStore());
        store.ToggleBookmark("/docs/a.md", 5);
        store.ToggleBookmark("/docs/a.md", 1);

        Assert.Equal(new[] { 1, 5 }, store.BookmarksFor("/docs/a.md"));
    }
}
