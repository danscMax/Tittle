using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tittle.Core.Abstractions;
using Tittle.Core.Services;

namespace Tittle.Platform;

/// <summary>MRU recent-files list persisted through <see cref="ISettingsStore"/>.</summary>
public sealed class RecentFilesStore : IRecentFilesStore
{
    private const string Key = "recent";

    private readonly ISettingsStore _settings;
    private readonly RecentFilesList _list;
    private readonly string _tempRoot;

    public event EventHandler? Changed;

    /// <param name="tempRoot">OS temp folder whose files are excluded from "Recent"; defaults to
    /// <see cref="Path.GetTempPath"/>. Overridable so tests can point at a scratch directory.</param>
    public RecentFilesStore(ISettingsStore settings, string? tempRoot = null)
    {
        _settings = settings;
        _tempRoot = tempRoot ?? Path.GetTempPath();
        // Drop entries whose file no longer exists (e.g. deleted temp files) AND entries living under
        // the OS temp folder (files opened from an archive / attachment), so the recent list never
        // surfaces dead or throwaway paths; prune them from persistence too.
        var loaded = _settings.Load<List<string>>(Key) ?? new List<string>();
        var existing = loaded
            .Where(p => File.Exists(p) && !RecentFilePathPolicy.IsUnderTempFolder(p, _tempRoot))
            .ToList();
        _list = new RecentFilesList(existing);
        if (existing.Count != loaded.Count)
            _settings.Save(Key, _list.Paths.ToList());
    }

    public IReadOnlyList<string> Items => _list.Paths;

    public void Add(string path)
    {
        // Never record a throwaway file opened from the OS temp folder.
        if (RecentFilePathPolicy.IsUnderTempFolder(path, _tempRoot))
            return;

        _list.Add(path);
        _settings.Save(Key, _list.Paths.ToList());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
