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

        try
        {
            if (OperatingSystem.IsWindows())
            {
                // A Windows path can't legitimately contain a double-quote or a control char; one that
                // does is malformed/hostile and would break out of the quoted /select, token to inject
                // further explorer switches. Fall back to opening the containing folder (ArgumentList,
                // no shell parsing) instead of interpolating it raw.
                if (filePath.Contains('"') || filePath.Any(char.IsControl))
                {
                    var folder = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(folder))
                        Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { folder } });
                    return;
                }

                // explorer's /select, is finicky about quoting — the raw argument string is the proven form.
                Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{filePath}\"" });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open") { ArgumentList = { "-R", filePath } });
            }
            else
            {
                var folder = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(folder))
                    Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { folder } });
            }
        }
        catch
        {
            // Best-effort: a missing file manager / unhandled path must not crash the viewer.
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
