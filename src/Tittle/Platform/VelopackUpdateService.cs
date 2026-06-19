using System;
using System.Threading;
using System.Threading.Tasks;
using Tittle.Core.Abstractions;
using Velopack;
using Velopack.Sources;

namespace Tittle.Platform;

/// <summary>
/// Velopack-backed <see cref="IUpdateService"/>. Reads releases from the GitHub Releases of the repo
/// (the same release the CI <c>vpk upload github</c> step publishes to). Inert for the portable
/// single-file build — <see cref="UpdateManager.IsInstalled"/> is false there, so every call short-circuits.
/// </summary>
public sealed class VelopackUpdateService : IUpdateService
{
    // The win-x64 Velopack channel lives on this repo's GitHub Releases. Public repo → no token;
    // stable releases only (prerelease: false).
    private const string RepoUrl = "https://github.com/danscMax/Tittle";

    private readonly UpdateManager _mgr = new(new GithubSource(RepoUrl, null, prerelease: false));
    private UpdateInfo? _pending;

    public bool IsSupported => _mgr.IsInstalled;

    public async Task<string?> CheckAndDownloadAsync(CancellationToken ct = default)
    {
        if (!_mgr.IsInstalled)
            return null;

        try
        {
            var info = await _mgr.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
                return null; // up to date

            await _mgr.DownloadUpdatesAsync(info, cancelToken: ct).ConfigureAwait(false);
            _pending = info;
            return info.TargetFullRelease.Version?.ToString();
        }
        catch
        {
            // Offline / GitHub unreachable / partial download — never surface as a crash; just no banner.
            return null;
        }
    }

    public void ApplyAndRestart()
    {
        if (_pending is null)
            return;
        _mgr.ApplyUpdatesAndRestart(_pending); // installs the staged update and relaunches; this process exits
    }
}
