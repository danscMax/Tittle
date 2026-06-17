using System;
using System.Collections.Generic;

namespace Tittle.Core.Abstractions;

/// <summary>Persistent most-recently-opened file list.</summary>
public interface IRecentFilesStore
{
    IReadOnlyList<string> Items { get; }

    void Add(string path);

    event EventHandler? Changed;
}
