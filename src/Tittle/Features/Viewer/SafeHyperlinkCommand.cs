using System;
using System.Diagnostics;
using System.Windows.Input;
using Tittle.Core.Text;

namespace Tittle.Features.Viewer;

/// <summary>
/// Opens markdown hyperlinks through the OS shell, but only for safe schemes
/// (http/https/mailto). Replaces Markdown.Avalonia's default command, which
/// shell-executes ANY scheme — including <c>file://</c> and custom protocol
/// handlers — without guarding. Link text comes straight from the (untrusted)
/// document, so it is validated via <see cref="MarkdownLink"/> before launching.
/// Stateless and shared via <see cref="Instance"/>.
/// </summary>
public sealed class SafeHyperlinkCommand : ICommand
{
    public static SafeHyperlinkCommand Instance { get; } = new();

    private SafeHyperlinkCommand() { }

    // The allow-list never changes, so executability is fixed; no notifications.
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    public bool CanExecute(object? parameter) => parameter is string url && MarkdownLink.IsSafe(url);

    public void Execute(object? parameter)
    {
        if (parameter is not string url || !MarkdownLink.IsSafe(url))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort: a broken or OS-unhandled link must not crash the viewer.
        }
    }
}
