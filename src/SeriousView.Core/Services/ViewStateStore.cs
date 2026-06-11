using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;

namespace SeriousView.Core.Services;

/// <summary>
/// In-memory holder for per-document reading state (visited headings + bookmarks, ported
/// md-visited-* / bookmarks), persisted as one <c>viewstate.json</c> via the same
/// <see cref="ISettingsStore"/> seam as the app settings. Mutations are cheap in-memory ops;
/// <see cref="Flush"/> writes once (call it where the session is saved). The file map is
/// LRU-pruned to <see cref="MaxFiles"/> entries by a monotonic touch counter.
/// </summary>
public sealed class ViewStateStore
{
    public const int MaxFiles = 200;
    private const string Key = "viewstate";

    private sealed class Entry
    {
        public HashSet<int> Visited { get; } = new();
        public SortedSet<int> Bookmarks { get; } = new();
        public long Touch { get; set; }
    }

    private readonly ISettingsStore _store;
    private readonly Dictionary<string, Entry> _files = new(StringComparer.OrdinalIgnoreCase);
    private long _touchCounter;
    private bool _dirty;

    public ViewStateStore(ISettingsStore store)
    {
        _store = store;
        var loaded = store.Load<ViewStateFile>(Key);
        if (loaded is null)
            return;

        foreach (var (path, state) in loaded.Files)
        {
            var entry = new Entry { Touch = state.Touch };
            entry.Visited.UnionWith(state.Visited);
            foreach (var b in state.Bookmarks)
                entry.Bookmarks.Add(b);
            _files[path] = entry;
            _touchCounter = Math.Max(_touchCounter, state.Touch);
        }
    }

    public bool IsVisited(string path, int ordinal)
        => _files.TryGetValue(Normalize(path), out var e) && e.Visited.Contains(ordinal);

    public bool IsBookmarked(string path, int ordinal)
        => _files.TryGetValue(Normalize(path), out var e) && e.Bookmarks.Contains(ordinal);

    /// <summary>Bookmarked ordinals for a document, ascending.</summary>
    public IReadOnlyList<int> BookmarksFor(string path)
        => _files.TryGetValue(Normalize(path), out var e) ? e.Bookmarks.ToList() : Array.Empty<int>();

    public void MarkVisited(string path, int ordinal)
    {
        if (ordinal < 0)
            return;
        _dirty |= TouchEntry(path).Visited.Add(ordinal);
    }

    /// <summary>Flips the bookmark; returns the NEW state (true = now bookmarked).</summary>
    public bool ToggleBookmark(string path, int ordinal)
    {
        var entry = TouchEntry(path);
        _dirty = true;
        if (entry.Bookmarks.Remove(ordinal))
            return false;
        entry.Bookmarks.Add(ordinal);
        return true;
    }

    /// <summary>Persists once if anything changed, pruning the least-recently-touched files.</summary>
    public void Flush()
    {
        if (!_dirty)
            return;
        _dirty = false;

        var kept = _files
            .OrderByDescending(kv => kv.Value.Touch)
            .Take(MaxFiles)
            .ToDictionary(
                kv => kv.Key,
                kv => new DocumentViewState
                {
                    Visited = kv.Value.Visited.ToList(),
                    Bookmarks = kv.Value.Bookmarks.ToList(),
                    Touch = kv.Value.Touch,
                },
                StringComparer.OrdinalIgnoreCase);
        _store.Save(Key, new ViewStateFile { Files = kept });
    }

    private Entry TouchEntry(string path)
    {
        var key = Normalize(path);
        if (!_files.TryGetValue(key, out var entry))
            _files[key] = entry = new Entry();
        entry.Touch = ++_touchCounter;
        return entry;
    }

    // Same normalization rule as tab reuse (FilePathEquality): full path, case-insensitive.
    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
