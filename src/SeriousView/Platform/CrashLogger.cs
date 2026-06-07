using System;
using System.IO;
using SeriousView.Core.Diagnostics;

namespace SeriousView.Platform;

/// <summary>
/// Appends unhandled-exception reports to <c>%AppData%/SeriousView/crash.log</c>. Standalone (no DI)
/// because it is wired in <see cref="Program.Main"/> before the app/container exists. Best-effort:
/// a crash handler must never throw, and the log is dropped once it grows past a small cap.
/// </summary>
public static class CrashLogger
{
    private const long MaxBytes = 256 * 1024;

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SeriousView", "crash.log");

    public static void Write(Exception error, string source)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

            // Simplest bound on growth: drop the old log once it gets large.
            var info = new FileInfo(LogPath);
            if (info.Exists && info.Length > MaxBytes)
                info.Delete();

            File.AppendAllText(LogPath, CrashLog.Format(DateTimeOffset.Now, error, source));
        }
        catch
        {
            // Diagnostics must never throw from a crash handler.
        }
    }
}
