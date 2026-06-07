namespace SeriousView.Core.Documents;

/// <summary>Size thresholds for graceful degradation on big files (tunable, UI-free).</summary>
public static class FileLimits
{
    /// <summary>Above this, syntax highlighting (TextMate) is suppressed but the file still opens.</summary>
    public const long HighlightMaxBytes = 5L * 1024 * 1024;   // 5 MB

    /// <summary>Above this, the file is not loaded into the editor at all.</summary>
    public const long LoadMaxBytes = 50L * 1024 * 1024;       // 50 MB

    public static bool IsTooLarge(long sizeBytes) => sizeBytes > LoadMaxBytes;

    public static bool SuppressHighlight(long sizeBytes) => sizeBytes > HighlightMaxBytes;
}
