using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Text;

namespace SeriousView.Core.Services;

/// <summary>Default <see cref="IFileReader"/>: reads bytes once, then classifies (too-large /
/// binary), detects the encoding, and normalizes line endings to LF.</summary>
public sealed class FileReader : IFileReader
{
    public async Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var size = new FileInfo(path).Length;          // throws if missing — caller reports it
        if (FileLimits.IsTooLarge(size))
            return FileLoadResult.TooLarge(size);

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (BinaryContent.IsProbablyBinary(bytes))
            return FileLoadResult.Binary(size);

        var (decoded, encodingName) = TextEncodingDetector.Decode(bytes);
        var lineEnding = LineEndings.Detect(decoded);
        return FileLoadResult.ForText(LineEndings.NormalizeToLf(decoded), encodingName, lineEnding, size);
    }
}
