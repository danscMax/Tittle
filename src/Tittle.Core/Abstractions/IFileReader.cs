using System.Threading;
using System.Threading.Tasks;
using Tittle.Core.Documents;

namespace Tittle.Core.Abstractions;

/// <summary>
/// Loads a file for viewing: detects encoding/binary/size and returns a
/// <see cref="FileLoadResult"/>. Abstracted so view models stay testable without the
/// real filesystem. Throws on genuine I/O errors (missing, locked, no access) — callers
/// translate those into user-facing messages.
/// </summary>
public interface IFileReader
{
    Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Re-read the file's raw bytes and decode them with a FORCED encoding (the «reinterpret as»
    /// path — fixes a mis-detected encoding), normalized to LF. Throws on I/O errors like
    /// <see cref="LoadAsync"/>.</summary>
    Task<string> ReloadTextAsync(string path, string encodingName, CancellationToken cancellationToken = default);
}
