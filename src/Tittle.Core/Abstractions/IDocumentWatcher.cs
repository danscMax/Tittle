namespace Tittle.Core.Abstractions;

/// <summary>What happened to a watched document on disk.</summary>
public enum DocumentChangeKind
{
    /// <summary>Content changed (or the file re-appeared under its name).</summary>
    Changed,

    /// <summary>Deleted or renamed away.</summary>
    Removed,
}

/// <summary>
/// Watches file-backed documents for EXTERNAL changes (M14 live-reload). Events are debounced
/// per path by the implementation (editor saves arrive as bursts — temp-write + replace dances)
/// and may be raised on ANY thread — consumers marshal to the UI thread themselves.
/// Implementations are fail-open: a path that cannot be watched (network drive quirks) simply
/// gets no live-reload, never an exception.
/// </summary>
public interface IDocumentWatcher : IDisposable
{
    void Watch(string path);

    void Unwatch(string path);

    event Action<string, DocumentChangeKind>? Changed;
}
