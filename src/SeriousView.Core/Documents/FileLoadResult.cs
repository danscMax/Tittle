namespace SeriousView.Core.Documents;

/// <summary>What kind of content a file load produced.</summary>
public enum FileLoadKind
{
    Text,
    Binary,
    TooLarge,
}

/// <summary>Outcome of loading a file for viewing. <see cref="Text"/> is normalized to LF and is
/// empty for non-text kinds; <see cref="HighlightSuppressed"/> is set for big-but-openable files.</summary>
public sealed record FileLoadResult(
    FileLoadKind Kind,
    string Text,
    string EncodingName,
    string LineEnding,
    long SizeBytes,
    bool HighlightSuppressed)
{
    public static FileLoadResult ForText(string text, string encodingName, string lineEnding, long sizeBytes)
        => new(FileLoadKind.Text, text, encodingName, lineEnding, sizeBytes, FileLimits.SuppressHighlight(sizeBytes));

    public static FileLoadResult Binary(long sizeBytes)
        => new(FileLoadKind.Binary, "", "", "", sizeBytes, false);

    public static FileLoadResult TooLarge(long sizeBytes)
        => new(FileLoadKind.TooLarge, "", "", "", sizeBytes, false);
}
