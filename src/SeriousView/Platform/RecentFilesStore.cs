using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;

namespace SeriousView.Platform;

/// <summary>MRU recent-files list persisted through <see cref="ISettingsStore"/>.</summary>
public sealed class RecentFilesStore : IRecentFilesStore
{
    private const string Key = "recent";

    private readonly ISettingsStore _settings;
    private readonly RecentFilesList _list;

    public event EventHandler? Changed;

    public RecentFilesStore(ISettingsStore settings)
    {
        _settings = settings;
        // Drop entries whose file no longer exists (e.g. deleted temp files) so the recent list
        // never surfaces dead paths; prune them from persistence too.
        var loaded = _settings.Load<List<string>>(Key) ?? new List<string>();
        var existing = loaded.Where(File.Exists).ToList();
        _list = new RecentFilesList(existing);
        if (existing.Count != loaded.Count)
            _settings.Save(Key, _list.Paths.ToList());
    }

    public IReadOnlyList<string> Items => _list.Paths;

    public void Add(string path)
    {
        _list.Add(path);
        _settings.Save(Key, _list.Paths.ToList());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
