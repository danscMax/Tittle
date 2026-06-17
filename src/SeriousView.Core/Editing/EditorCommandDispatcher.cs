using System;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Text;

namespace SeriousView.Core.Editing;

/// <summary>Applies an <see cref="IEditorIntent"/> to an <see cref="IEditorActions"/> surface — the
/// single seam every editor operation flows through, so the Phase-2 macro engine can record by
/// observing and replay by re-dispatching here. Pure of UI; tested with a fake surface.</summary>
public static class EditorCommandDispatcher
{
    public static void Apply(IEditorActions actions, IEditorIntent intent)
    {
        switch (intent)
        {
            case TransformLinesIntent t:
                ApplyLineOp(actions, t.Op);
                break;
        }
    }

    private static void ApplyLineOp(IEditorActions actions, LineOp op)
    {
        var text = actions.Text;
        var (selStart, selLen) = actions.Selection;

        if (IsRangeOp(op))
        {
            // Move / duplicate / join shift whole lines — operate on the full document by line index.
            var (startLine, endLine) = TouchedLineRange(text, selStart, selLen);
            var result = ApplyRangeOp(text, startLine, endLine, op);
            if (!string.Equals(result, text, StringComparison.Ordinal))
                actions.Replace(0, text.Length, result);
            return;
        }

        // Sort / dedup / trim / case: the selected whole lines, or the whole document when no selection.
        if (selLen > 0)
        {
            var (blockStart, blockLen) = SelectedLinesSpan(text, selStart, selLen);
            var block = text.Substring(blockStart, blockLen);
            var transformed = ApplyWholeOp(block, op);
            if (!string.Equals(transformed, block, StringComparison.Ordinal))
                actions.Replace(blockStart, blockLen, transformed);
        }
        else
        {
            var transformed = ApplyWholeOp(text, op);
            if (!string.Equals(transformed, text, StringComparison.Ordinal))
                actions.Replace(0, text.Length, transformed);
        }
    }

    private static bool IsRangeOp(LineOp op)
        => op is LineOp.MoveUp or LineOp.MoveDown or LineOp.Duplicate or LineOp.Join;

    private static string ApplyWholeOp(string block, LineOp op) => op switch
    {
        LineOp.SortAscending => LineOperations.Sort(block),
        LineOp.SortDescending => LineOperations.Sort(block, descending: true),
        LineOp.RemoveDuplicates => LineOperations.RemoveDuplicateLines(block),
        LineOp.TrimTrailing => LineOperations.TrimTrailing(block),
        LineOp.Upper => LineOperations.ChangeCase(block, CaseKind.Upper),
        LineOp.Lower => LineOperations.ChangeCase(block, CaseKind.Lower),
        LineOp.Title => LineOperations.ChangeCase(block, CaseKind.Title),
        _ => block,
    };

    private static string ApplyRangeOp(string text, int startLine, int endLine, LineOp op) => op switch
    {
        LineOp.MoveUp => LineOperations.MoveLines(text, startLine, endLine, -1),
        LineOp.MoveDown => LineOperations.MoveLines(text, startLine, endLine, +1),
        LineOp.Duplicate => LineOperations.DuplicateLines(text, startLine, endLine),
        LineOp.Join => LineOperations.JoinLines(text, startLine, endLine),
        _ => text,
    };

    /// <summary>Char span [start, start+length) covering every whole line the selection touches.</summary>
    private static (int Start, int Length) SelectedLinesSpan(string text, int selStart, int selLen)
    {
        var s = Math.Clamp(selStart, 0, text.Length);
        var e = Math.Clamp(selStart + Math.Max(0, selLen), s, text.Length);
        var lineStart = s == 0 ? 0 : text.LastIndexOf('\n', s - 1) + 1;
        var probe = selLen > 0 ? Math.Max(s, e - 1) : e; // last char actually touched
        var nl = probe < text.Length ? text.IndexOf('\n', probe) : -1;
        var lineEnd = nl < 0 ? text.Length : nl; // exclusive of the '\n'
        return (lineStart, lineEnd - lineStart);
    }

    /// <summary>0-based inclusive line indices the selection touches.</summary>
    private static (int StartLine, int EndLine) TouchedLineRange(string text, int selStart, int selLen)
    {
        var s = Math.Clamp(selStart, 0, text.Length);
        var e = Math.Clamp(selStart + Math.Max(0, selLen), s, text.Length);
        var probe = selLen > 0 ? Math.Max(s, e - 1) : e;
        return (LineIndexOf(text, s), LineIndexOf(text, probe));
    }

    private static int LineIndexOf(string text, int offset)
    {
        var count = 0;
        var limit = Math.Min(offset, text.Length);
        for (var i = 0; i < limit; i++)
            if (text[i] == '\n')
                count++;
        return count;
    }
}
