namespace SeriousView.Core.Text;

/// <summary>Pure text metrics for the status bar (testable, UI-free).</summary>
public static class TextMetrics
{
    /// <summary>Number of lines (a trailing newline counts as starting a new line).</summary>
    public static int LineCount(string text)
        => string.IsNullOrEmpty(text) ? 0 : text.AsSpan().Count('\n') + 1;

    /// <summary>Character count; null-safe to match <see cref="LineCount"/>.</summary>
    public static int CharCount(string? text) => text?.Length ?? 0;
}
