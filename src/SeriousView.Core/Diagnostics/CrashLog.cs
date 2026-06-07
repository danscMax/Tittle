using System;
using System.Globalization;
using System.Text;

namespace SeriousView.Core.Diagnostics;

/// <summary>
/// Formats an unhandled exception into a single human-readable crash-log entry. Pure (no I/O) so it
/// is unit-testable; the file append lives in the UI layer (<c>Platform/CrashLogger</c>).
/// </summary>
public static class CrashLog
{
    /// <summary>One entry: a separator with UTC timestamp + source, then the exception chain.</summary>
    public static string Format(DateTimeOffset when, Exception error, string source)
    {
        var timestamp = when.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        sb.Append("==== ").Append(timestamp).Append(" UTC · ").Append(source).AppendLine(" ====");
        sb.Append(error.GetType().FullName).Append(": ").AppendLine(error.Message);

        if (error.StackTrace is { Length: > 0 } stack)
            sb.AppendLine(stack);

        // Inner exceptions often carry the real cause; include the chain.
        for (var inner = error.InnerException; inner is not null; inner = inner.InnerException)
            sb.Append("--- inner: ").Append(inner.GetType().FullName).Append(": ").AppendLine(inner.Message);

        return sb.ToString();
    }
}
