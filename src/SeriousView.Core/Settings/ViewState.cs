using System.Collections.Generic;

namespace SeriousView.Core.Settings;

/// <summary>Per-document reading state (ported md-visited-* / bookmarks): which headings
/// were seen and which are bookmarked, both by outline ordinal. <see cref="Touch"/> is a
/// monotonic recency stamp for the LRU prune — not wall-clock time.</summary>
public sealed record DocumentViewState
{
    public List<int> Visited { get; init; } = new();

    public List<int> Bookmarks { get; init; } = new();

    public long Touch { get; init; }
}

/// <summary>The persisted viewstate.json shape: normalized path → per-document state.</summary>
public sealed record ViewStateFile
{
    public int SchemaVersion { get; init; } = 1;

    public Dictionary<string, DocumentViewState> Files { get; init; } = new();
}
