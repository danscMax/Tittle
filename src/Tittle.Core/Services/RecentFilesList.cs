using System;
using System.Collections.Generic;
using System.Linq;

namespace Tittle.Core.Services;

/// <summary>
/// Pure MRU (most-recently-used) list of file paths: newest first, case-insensitive
/// de-duplication, capped length. UI-free and unit-tested.
/// </summary>
public sealed class RecentFilesList
{
    private readonly List<string> _paths;
    private readonly int _cap;

    public RecentFilesList(IEnumerable<string>? initial = null, int cap = 10)
    {
        _cap = cap;
        _paths = (initial ?? Enumerable.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Take(cap)
            .ToList();
    }

    public IReadOnlyList<string> Paths => _paths;

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _paths.Insert(0, path);
        // A single insert can overflow the cap by at most one, so one trim suffices.
        if (_paths.Count > _cap)
            _paths.RemoveAt(_paths.Count - 1);
    }

    public void Remove(string path)
        => _paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
}
