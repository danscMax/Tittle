using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>
/// <see cref="IShellService"/> backed by the OS file manager. Reveal-and-select is platform-specific:
/// Windows <c>explorer /select,</c>, macOS <c>open -R</c>; Linux has no standard reveal, so we open the
/// containing folder with <c>xdg-open</c>. Best-effort — a missing tool or unhandled path is swallowed
/// (same policy as <c>SafeHyperlinkCommand</c>), never crashing the viewer.
/// </summary>
public sealed class ShellService : IShellService
{
    public void RevealInExplorer(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var platform = OperatingSystem.IsWindows() ? RevealPlatform.Windows
            : OperatingSystem.IsMacOS() ? RevealPlatform.MacOS
            : RevealPlatform.Linux;

        try
        {
            if (BuildRevealStartInfo(filePath, platform) is { } psi)
                Process.Start(psi);
        }
        catch
        {
            // Best-effort: a missing file manager / unhandled path must not crash the viewer.
        }
    }

    internal enum RevealPlatform
    {
        Windows,
        MacOS,
        Linux,
    }

    /// <summary>Builds the reveal-in-file-manager <see cref="ProcessStartInfo"/> for a platform, or
    /// <c>null</c> for a no-op (a path with no containing folder where we'd fall back to one). Pure —
    /// no <see cref="Process"/> launch and no OS probe — so a test can drive every platform branch on
    /// any host. The <paramref name="platform"/> is resolved by the caller.</summary>
    internal static ProcessStartInfo? BuildRevealStartInfo(string filePath, RevealPlatform platform)
    {
        switch (platform)
        {
            case RevealPlatform.Windows:
                // A Windows path can't legitimately contain a double-quote or a control char; one that
                // does is malformed/hostile and would break out of the quoted /select, token to inject
                // further explorer switches. Fall back to opening the containing folder (ArgumentList,
                // no shell parsing) instead of interpolating it raw.
                if (filePath.Contains('"') || filePath.Any(char.IsControl))
                {
                    var folder = Path.GetDirectoryName(filePath);
                    return string.IsNullOrEmpty(folder)
                        ? null
                        : new ProcessStartInfo("explorer.exe") { ArgumentList = { folder } };
                }

                // explorer's /select, is finicky about quoting and must stay a single raw argument
                // STRING: passing "/select,<path>" through ArgumentList would quote the whole token
                // ("/select,C:\…") on a path with spaces and break the selection. The guard above
                // already rules out the only injection vector (a quote / control char in the path),
                // and the path is a real opened file, never content-derived — so the string is safe.
                return new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{filePath}\"" };

            case RevealPlatform.MacOS:
                return new ProcessStartInfo("open") { ArgumentList = { "-R", filePath } };

            default: // Linux: no standard reveal — open the containing folder.
                var dir = Path.GetDirectoryName(filePath);
                return string.IsNullOrEmpty(dir)
                    ? null
                    : new ProcessStartInfo("xdg-open") { ArgumentList = { dir } };
        }
    }

    public void OpenWithDefaultApp(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        try
        {
            // UseShellExecute routes through the OS file association (browser for .html).
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort: no association / sandboxed shell must not crash the viewer.
        }
    }
}
