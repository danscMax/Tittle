using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using SeriousView.Core.Abstractions;

namespace SeriousView.Platform;

/// <summary>
/// <see cref="IClipboardService"/> backed by Avalonia's <c>TopLevel.Clipboard</c>. The TopLevel is
/// resolved lazily through the same <see cref="Func{TResult}"/> the file picker uses, so it is available
/// after the window is shown. A null clipboard (no window yet / headless) makes the call a no-op.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    private readonly Func<TopLevel?> _topLevel;

    public ClipboardService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task SetTextAsync(string text)
    {
        var clipboard = _topLevel()?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
