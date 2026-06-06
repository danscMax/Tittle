using System;
using System.Collections.Generic;
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
        _list = new RecentFilesList(_settings.Load<List<string>>(Key));
    }

    public IReadOnlyList<string> Items => _list.Paths;

    public void Add(string path)
    {
        _list.Add(path);
        _settings.Save(Key, _list.Paths.ToList());
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
