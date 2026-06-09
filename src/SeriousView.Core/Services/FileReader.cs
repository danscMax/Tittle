using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Documents;
using SeriousView.Core.Text;

namespace SeriousView.Core.Services;

/// <summary>Default <see cref="IFileReader"/>: classifies (too-large / binary) from the file head,
/// then for text reads the whole file, detects the encoding, and normalizes line endings to LF.</summary>
public sealed class FileReader : IFileReader
{
    public async Task<FileLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var size = new FileInfo(path).Length;          // throws if missing — caller reports it
        if (FileLimits.IsTooLarge(size))
            return FileLoadResult.TooLarge(size);

        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true);

        // Classify from an exactly-sized head — BinaryContent only scans the first ScanWindow bytes,
        // so a binary file is rejected without reading the rest. The head must be exactly the bytes
        // read: a zero-padded tail would look like NUL bytes and mis-flag a short text file as binary.
        var headLength = (int)Math.Min(BinaryContent.ScanWindow, size);
        var head = new byte[headLength];
        await stream.ReadExactlyAsync(head.AsMemory(), cancellationToken).ConfigureAwait(false);
        if (BinaryContent.IsProbablyBinary(head))
            return FileLoadResult.Binary(size);

        // Text needs the whole file (UTF-8 validation + the content itself). Reuse the head and read
        // the remainder into the tail — one allocation, and the head bytes are not read twice.
        var bytes = new byte[size];
        head.CopyTo(bytes, 0);
        if (size > headLength)
            await stream.ReadExactlyAsync(bytes.AsMemory(headLength), cancellationToken).ConfigureAwait(false);

        var (decoded, encodingName) = TextEncodingDetector.Decode(bytes);
        var lineEnding = LineEndings.Detect(decoded);
        return FileLoadResult.ForText(LineEndings.NormalizeToLf(decoded), encodingName, lineEnding, size);
    }
}
