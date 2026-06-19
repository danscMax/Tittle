using Avalonia;
using Tittle.Platform;
using Velopack;

namespace Tittle;

internal static class Program
{
    /// <summary>The primary instance's single-instance gate, handed to <see cref="App"/> after the
    /// window exists so it can start the pipe server and route forwarded file opens. Null in the
    /// (already-exited) secondary. Owned by <see cref="Main"/>; disposed in its finally.</summary>
    internal static SingleInstanceGate? Gate { get; private set; }

    // One-time rename migration (SeriousView → Tittle): if the new per-user data dir doesn't exist
    // yet but the legacy one does, copy it over so an existing install keeps its settings, session,
    // recent files, macros and view state. Best-effort — a failure must never block startup (a fresh
    // data dir is an acceptable fallback). The legacy name is a frozen literal, not AppPaths.
    private static void MigrateLegacyDataDir()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var legacy = Path.Combine(appData, "SeriousView");
            var current = AppPaths.DataDir;
            if (Directory.Exists(current) || !Directory.Exists(legacy))
                return;

            Directory.CreateDirectory(current);
            foreach (var src in Directory.EnumerateFiles(legacy, "*", SearchOption.AllDirectories))
            {
                var dst = Path.Combine(current, Path.GetRelativePath(legacy, src));
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: false);
            }
        }
        catch
        {
            // best-effort: a fresh Tittle data dir is fine.
        }
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't
    // initialized yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack MUST run first: on an install/update/uninstall hook it does its work and exits the
        // process immediately — before the single-instance gate or Avalonia ever start. For a portable
        // (non-Velopack) launch this is a silent no-op, so the single-file build is unaffected.
        VelopackApp.Build().Run();

        // Carry settings/session/recents/macros over from the pre-rename install, before anything
        // touches the (new) data dir.
        MigrateLegacyDataDir();

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
            // Bold/italic font fix — replaces .WithInterFont(). Avalonia 11.3.x has an open regression
            // (#18875) that mis-resolves the weight of VARIABLE fonts, so FontWeight=Bold renders at
            // normal weight everywhere — and Avalonia.Fonts.Inter ships Inter as a variable font. We
            // register STATIC per-weight/style Inter faces and pin "Inter" as the default (see
            // Platform/InterFontCollection). The SAME call runs in the test + render harnesses.
            .WithBundledInterFont()
            .LogToTrace();
}
