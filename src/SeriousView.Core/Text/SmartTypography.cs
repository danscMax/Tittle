using System.Text.RegularExpressions;

namespace SeriousView.Core.Text;

/// <summary>
/// Display-only smart typography for plain-text files (ported): <c> -- </c> → em dash,
/// <c> -> </c>/<c> => </c> → arrows, <c>...</c> → ellipsis, straight quotes → «guillemets».
/// Line-by-line with a code-likeness guard (a line containing code keywords or operator soup
/// is left alone), and the line count never changes — so outline line numbers stay valid.
/// The stored document text is never touched.
/// </summary>
public static partial class SmartTypography
{
    public static string Apply(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || line.Length > 2000 || CodeLike().IsMatch(line))
                continue;

            line = line
                .Replace(" -- ", " — ")
                .Replace(" -> ", " → ")
                .Replace(" => ", " ⇒ ")
                .Replace("...", "…");
            line = Quoted().Replace(line, "«$1»");
            lines[i] = line;
        }

        return string.Join("\n", lines);
    }

    // Lines that smell like code/shell are exempt (the original viewer's CODE_LIKE guard,
    // minus the bare "=>": it made the " => " → " ⇒ " replacement unreachable; arrow
    // functions still trip the brace/semicolon/keyword signals).
    [GeneratedRegex(@"function|const |let |var |return|if \(|else|for \(|while \(|class |[{};]|&&|\|\||==|!=|<=|>=")]
    private static partial Regex CodeLike();

    // A straight-quoted phrase on one line; lazy body without quotes.
    [GeneratedRegex("\"([^\"\n]+)\"")]
    private static partial Regex Quoted();
}
