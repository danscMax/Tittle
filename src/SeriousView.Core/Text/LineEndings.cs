namespace SeriousView.Core.Text;

/// <summary>Line-ending detection and normalization (UI-free, testable). Normalizing to LF keeps
/// downstream line counting / parsing correct for CRLF and old-Mac CR-only files.</summary>
public static class LineEndings
{
    /// <summary>Dominant line ending: "LF", "CRLF", "CR", "Mixed", or "" when the text has no breaks.</summary>
    public static string Detect(string text)
    {
        int crlf = 0, cr = 0, lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                else cr++;
            }
            else if (text[i] == '\n')
            {
                lf++;
            }
        }

        var kinds = (crlf > 0 ? 1 : 0) + (cr > 0 ? 1 : 0) + (lf > 0 ? 1 : 0);
        return kinds switch
        {
            0 => "",
            > 1 => "Mixed",
            _ => crlf > 0 ? "CRLF" : cr > 0 ? "CR" : "LF",
        };
    }

    /// <summary>Normalize all CRLF / CR line endings to LF.</summary>
    public static string NormalizeToLf(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
