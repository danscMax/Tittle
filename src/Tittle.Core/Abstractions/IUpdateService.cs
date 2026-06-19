using System.Threading;
using System.Threading.Tasks;

namespace Tittle.Core.Abstractions;

/// <summary>
/// Application self-update (installer + delta auto-update). The implementation (Velopack) lives in the
/// UI layer; Core only knows this Velopack-agnostic contract so the shell VM's update-banner logic is
/// headless-testable with a fake. Inert for the portable single-file build (<see cref="IsSupported"/>
/// is false there).
/// </summary>
public interface IUpdateService
{
    /// <summary>True only when the app was installed through the updater (Velopack). False for the
    /// portable single-file build — callers must skip all update work when this is false.</summary>
    bool IsSupported { get; }

    /// <summary>Check for a newer release and, if found, download it (deltas where possible). Returns the
    /// new version string when an update is downloaded and ready to apply; null when up to date or
    /// unsupported. Never throws — a network/offline failure returns null.</summary>
    Task<string?> CheckAndDownloadAsync(CancellationToken ct = default);

    /// <summary>Apply the update downloaded by the last <see cref="CheckAndDownloadAsync"/> and restart
    /// the app. The process exits; callers should persist state first. No-op if nothing is pending.</summary>
    void ApplyAndRestart();
}
