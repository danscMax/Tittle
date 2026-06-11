using System.Collections.Generic;

namespace SeriousView.Core.Text;

/// <summary>
/// Flips the N-th task-list checkbox in RAW markdown (M15 checkbox click-to-toggle).
/// The preview renders the preprocessor's ☐/☑ glyphs in document order and fenced lines are
/// skipped on BOTH sides (the same <see cref="MarkdownCodeRegions"/> guard), so the N-th glyph
/// the user clicks maps to the N-th task line found here.
/// </summary>
public static class TaskListToggle
{
    /// <summary>Returns the document with the <paramref name="taskIndex"/>-th checkbox
    /// flipped, or null when there is no such task item. Line endings are normalized to LF —
    /// the same normalization every loaded document already has.</summary>
    public static string? ToggleAt(string? documentText, int taskIndex)
    {
        if (string.IsNullOrEmpty(documentText) || taskIndex < 0)
            return null;

        var lines = new List<string>(LineEndings.NormalizeToLf(documentText).Split('\n'));
        var regions = MarkdownCodeRegions.Scan(lines);

        var seen = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (regions.IsFencedLine(i))
                continue; // "- [x]" inside a fence is code, and the preview shows no glyph for it

            var item = MarkdownPreprocessor.TaskItem().Match(lines[i]);
            if (!item.Success || seen++ != taskIndex)
                continue;

            var flipped = item.Groups[2].Value == " " ? "x" : " ";
            lines[i] = $"{item.Groups[1].Value}[{flipped}] {item.Groups[3].Value}";
            return string.Join("\n", lines);
        }

        return null;
    }
}
