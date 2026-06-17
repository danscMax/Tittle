using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Tittle.Core.Diagnostics;

namespace Tittle.Platform;

/// <summary>
/// Per-user single-instance gate (BCL only: Mutex + NamedPipe), constructed in <c>Program.Main</c>
/// before Avalonia/DI exist (like <see cref="CrashLogger"/>). Primary: <see cref="IsPrimary"/> is true;
/// call <see cref="StartServer"/> after the window exists and handle <see cref="FileOpenRequested"/>.
/// Secondary: <see cref="IsPrimary"/> is false; call <see cref="TryForward"/> then exit. Fail-open: any
/// error (macOS, sandbox, …) → treat self as primary WITHOUT a server, i.e. keep the current
/// multi-instance behaviour rather than crash (hard CI constraint).
/// </summary>
public sealed class SingleInstanceGate : IDisposable
{
    private readonly string _pipeName;
    private readonly Mutex? _mutex;
    private readonly bool _owns;
    private CancellationTokenSource? _serverCts;
    private volatile bool _disposed;

    public bool IsPrimary { get; }

    /// <summary>Raised on a background thread when a secondary forwards file paths. Subscribers MUST
    /// marshal to the UI thread (App does, via Dispatcher.UIThread.Post).</summary>
    public event Action<IReadOnlyList<string>>? FileOpenRequested;

    public SingleInstanceGate()
    {
        var user = Environment.UserName;
        _pipeName = SingleInstanceMessage.PipeName(user);
        try
        {
            _mutex = new Mutex(initiallyOwned: true, SingleInstanceMessage.MutexName(user), out var createdNew);
            _owns = createdNew;
            IsPrimary = createdNew;
        }
        catch
        {
            _mutex = null;
            _owns = false;
            IsPrimary = true; // fail open: standalone, no server
        }
    }

    /// <summary>Secondary: connect to the primary's pipe and send the args. Returns false if the primary
    /// is gone / mid-shutdown so the caller can fall back to launching its own window.</summary>
    public bool TryForward(IReadOnlyList<string> args)
    {
        var payload = SingleInstanceMessage.Encode(args);
        // The primary accepts one connection at a time and re-listens within milliseconds; a
        // near-simultaneous second launch can momentarily find the single server instance busy and
        // time out. Retry a few short connects before giving up — a genuinely gone primary surfaces
        // as a non-timeout error and bails immediately. CurrentUserOnly matches the server's DACL.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(
                    ".", _pipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
                client.Connect(1000);
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch (TimeoutException)
            {
                // Server busy with another forward — retry.
            }
            catch
            {
                return false; // primary gone / pipe error — let the caller fall back to its own window
            }
        }
        return false;
    }

    /// <summary>Primary: begin accepting forwarded messages on a background loop. Never throws.</summary>
    public void StartServer()
    {
        if (!IsPrimary || _serverCts is not null)
            return;
        try
        {
            _serverCts = new CancellationTokenSource();
            _ = Task.Run(() => ServerLoopAsync(_serverCts.Token));
        }
        catch
        {
            _serverCts = null;
        }
    }

    private async Task ServerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Asynchronous so the CancellationToken can unblock WaitForConnectionAsync/CopyToAsync on
                // Dispose (cancellation is honored only for async pipes). CurrentUserOnly restricts the
                // pipe DACL to the current user (Windows) / 0600 socket (Unix), rejecting other users and
                // elevation levels — the BCL answer to a hostile local connector injecting file opens.
                using var server = new NamedPipeServerStream(
                    _pipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var ms = new MemoryStream();
                await server.CopyToAsync(ms, ct).ConfigureAwait(false); // read until the client closes
                var paths = SingleInstanceMessage.Decode(ms.GetBuffer().AsSpan(0, (int)ms.Length));
                if (paths.Count > 0)
                    FileOpenRequested?.Invoke(paths);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // One bad connection must not kill the loop.
                try { await Task.Delay(50, ct).ConfigureAwait(false); }
                catch { break; }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        // Cancelling the CTS unblocks the async WaitForConnectionAsync/CopyToAsync and ends the loop
        // (the pipe is created with PipeOptions.Asynchronous, so token cancellation is honored).
        try { _serverCts?.Cancel(); } catch { /* ignore */ }
        try { _serverCts?.Dispose(); } catch { /* ignore */ }
        try { if (_owns) _mutex?.ReleaseMutex(); } catch { /* ignore */ }
        try { _mutex?.Dispose(); } catch { /* ignore */ }
    }
}
