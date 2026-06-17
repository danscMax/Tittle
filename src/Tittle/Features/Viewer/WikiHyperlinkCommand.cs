using System;
using System.IO;
using System.Windows.Input;
using Tittle.Core.Text;

namespace Tittle.Features.Viewer;

/// <summary>
/// Per-view hyperlink command for the markdown preview (M10): <c>wiki:</c> links open the
/// sibling note (<c>&lt;name&gt;.md</c> in the document's folder) through the shell's open
/// funnel; every other URL is delegated verbatim to the safe fallback
/// (<see cref="SafeHyperlinkCommand"/> — http/https/mailto only), so the existing policy is
/// not weakened. Defence in depth on the wiki branch: the decoded name is re-validated, the
/// resolved path must stay under the asset root (literal-prefix check — the candidate is built
/// from the root), the target must exist, and only <c>.md</c> can ever be addressed. A wiki
/// link never reaches <c>Process.Start</c>.
/// </summary>
public sealed class WikiHyperlinkCommand(
    Func<string?> assetRootProvider,
    Action<string> openPath,
    ICommand fallback) : ICommand
{
    // Executability is static per URL shape; no notifications needed.
    public event EventHandler? CanExecuteChanged { add { } remove { } }

    /// <summary>Wiki: syntactic validity + a known asset root — deliberately NO File.Exists
    /// here (this runs per hover/render); existence is checked at Execute time.</summary>
    public bool CanExecute(object? parameter)
        => parameter is string url
           && (WikiLink.IsWikiUrl(url)
               ? WikiLink.TryGetName(url, out _) && assetRootProvider() is not null
               : fallback.CanExecute(url));

    public void Execute(object? parameter)
    {
        if (parameter is not string url)
            return;

        if (!WikiLink.IsWikiUrl(url))
        {
            fallback.Execute(url);
            return;
        }

        try
        {
            if (!WikiLink.TryGetName(url, out var name) || assetRootProvider() is not { } root)
                return;

            var rootFull = Path.GetFullPath(root);
            var candidate = Path.GetFullPath(Path.Combine(rootFull, name + ".md"));
            if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !candidate.StartsWith(rootFull + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
                return;

            // The transform's existence snapshot may be stale (file deleted since) — a missing
            // target is a silent no-op, never an error.
            if (File.Exists(candidate))
                openPath(candidate);
        }
        catch
        {
            // A hostile or malformed link must not crash the viewer (mirrors SafeHyperlinkCommand).
        }
    }
}
