namespace SeriousView.Core.Text;

/// <summary>A target line-ending style for <see cref="LineEndings.ConvertTo"/>.</summary>
public enum Eol
{
    Lf,
    CrLf,
    Cr,
}

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

    /// <summary>Normalize all CRLF / CR line endings to LF. When the text is already LF-only
    /// (no '\r' at all — the common Unix/markdown case), the same reference is returned without
    /// allocating a copy; a full-document scan/copy near the size ceiling is wasted otherwise.</summary>
    public static string NormalizeToLf(string text)
        => text.IndexOf('\r') < 0
            ? text
            : text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>Rewrite every line ending to a single target style — normalize to LF first (so mixed
    /// input becomes uniform), then expand to the target. The <see cref="Eol.Lf"/> case is the
    /// normalization itself.</summary>
    public static string ConvertTo(string text, Eol eol)
    {
        var lf = NormalizeToLf(text);
        return eol switch
        {
            Eol.CrLf => lf.Replace("\n", "\r\n"),
            Eol.Cr => lf.Replace('\n', '\r'),
            _ => lf,
        };
    }
}
