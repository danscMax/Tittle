namespace Tittle.Core.Text;

/// <summary>The <c>wiki:</c> URL policy shared by the preprocessor (link creation) and the
/// viewer's hyperlink command (click-time validation). Pure and deterministic across platforms —
/// no <c>System.IO.Path</c> (its separator rules differ per OS); separators, traversal, control
/// characters and a leading <c>^</c> (which would collide with the footnote-reference regex
/// downstream) are all rejected explicitly. A wiki name is just a sibling note's file name
/// without the <c>.md</c> extension.</summary>
public static class WikiLink
{
    public const string UrlPrefix = "wiki:";

    private static readonly char[] ForbiddenChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static bool IsWikiUrl(string? url)
        => url?.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase) == true;

    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "." || name.StartsWith('^'))
            return false;
        if (name.Contains(".."))
            return false;
        if (name.IndexOfAny(ForbiddenChars) >= 0)
            return false;

        foreach (var ch in name)
        {
            if (char.IsControl(ch))
                return false;
        }

        return true;
    }

    /// <summary>Builds <c>wiki:&lt;percent-encoded name&gt;</c> — fully ASCII, no spaces or
    /// parens, so the markdown renderer's link-destination parser accepts it verbatim.</summary>
    public static string CreateUrl(string name) => UrlPrefix + Uri.EscapeDataString(name);

    /// <summary>Extracts and validates the DECODED name, so percent-encoded traversal
    /// (<c>wiki:%2e%2e%2f…</c>) is caught the same as a literal one.</summary>
    public static bool TryGetName(string? url, out string name)
    {
        name = string.Empty;
        if (url is null || !IsWikiUrl(url))
            return false;

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(url[UrlPrefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (!IsValidName(decoded))
            return false;

        name = decoded;
        return true;
    }
}
