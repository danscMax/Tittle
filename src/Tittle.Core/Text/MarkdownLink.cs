using System;

namespace Tittle.Core.Text;

/// <summary>Link-safety policy for rendered markdown (UI-free, testable).
/// A viewer opens links from untrusted documents, so it must not hand arbitrary
/// schemes (file, javascript, custom protocol handlers) to the OS shell. Only
/// web and mail links are allowed through; the shell-execute itself stays in the UI.</summary>
public static class MarkdownLink
{
    /// <summary>True only for an absolute URL with a scheme safe to open from a viewer:
    /// <c>http</c>, <c>https</c> or <c>mailto</c> (<see cref="Uri.Scheme"/> is lower-cased).
    /// Null/empty/relative/other → false.</summary>
    public static bool IsSafe(string? url)
        => !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp
            || uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeMailto);
}
