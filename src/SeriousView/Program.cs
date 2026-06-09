using Avalonia;
using SeriousView.Platform;

namespace SeriousView;

internal static class Program
{
    /// <summary>The primary instance's single-instance gate, handed to <see cref="App"/> after the
    /// window exists so it can start the pipe server and route forwarded file opens. Null in the
    /// (already-exited) secondary. Owned by <see cref="Main"/>; disposed in its finally.</summary>
    internal static SingleInstanceGate? Gate { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't
    // initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Last-resort diagnostics: log unhandled exceptions before the process dies.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                CrashLogger.Write(ex, "AppDomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogger.Write(e.Exception, "TaskScheduler");
            e.SetObserved();
        };

        // Single-instance: if another instance already owns the gate, forward our file args to it and
        // exit WITHOUT starting Avalonia. If forwarding fails (the primary was mid-shutdown), fall
        // through and become the primary ourselves so the file is never lost. Fail-open by design.
        var gate = new SingleInstanceGate();
        if (!gate.IsPrimary)
        {
            if (gate.TryForward(args))
            {
                gate.Dispose();
                return;
            }
            // Forward failed: the primary may have vanished mid-shutdown (then we should take over), or
            // it is alive but was momentarily unreachable. Re-create and re-check — if the re-created gate
            // is still NOT primary, a live primary holds the mutex, so try forwarding once more rather than
            // opening a deaf second window. Only fall through (become a best-effort extra instance) if we
            // neither acquired the mutex nor could forward — fail-open, never lose the file.
            gate.Dispose();
            gate = new SingleInstanceGate();
            if (!gate.IsPrimary && gate.TryForward(args))
            {
                gate.Dispose();
                return;
            }
        }

        Gate = gate;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            gate.Dispose(); // release the mutex + stop the pipe server on normal or abnormal exit
        }
    }

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
