using System.Threading;
using System.Threading.Tasks;
using SeriousView.Core.Documents;

namespace SeriousView.Core.Abstractions;

/// <summary>
/// Loads a file for viewing: detects encoding/binary/size and returns a
/// <see cref="FileLoadResult"/>. Abstracted so view models stay testable without the
/// real filesystem. Throws on genuine I/O errors (missing, locked, no access) — callers
/// translate those into user-facing messages.
/// </summary>
public interface IFileReader
{
    Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default);
}
