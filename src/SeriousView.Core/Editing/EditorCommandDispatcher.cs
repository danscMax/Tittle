using System;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Text;

namespace SeriousView.Core.Editing;

/// <summary>Applies an <see cref="IEditorIntent"/> to an <see cref="IEditorActions"/> surface — the
/// single seam every editor operation flows through, so the Phase-2 macro engine can record by
/// observing and replay by re-dispatching here. Pure of UI; tested with a fake surface.</summary>
public static class EditorCommandDispatcher
{
    /// <summary>Apply one intent. Returns whether it made PROGRESS — most intents return true; a
    /// <see cref="FindNextIntent"/> that found nothing at/after the caret (EOF) and a no-op delete at a
    /// boundary return false, which is what ends an until-no-match macro replay.</summary>
    public static bool Apply(IEditorActions actions, IEditorIntent intent)
    {
        switch (intent)
        {
            case TransformLinesIntent t:
                ApplyLineOp(actions, t.Op);
                return true;
            case ConvertEolIntent c:
                ApplyConvertEol(actions, c.Target);
                return true;
            case InsertTextIntent ins:
            {
                var (start, length) = actions.Selection;
                actions.Replace(start, length, ins.Text);
                return true;
            }

            case ReplaceSelectionIntent rep:
            {
                var (start, length) = actions.Selection;
                actions.Replace(start, length, rep.Text);
                return true;
            }

            case DeleteTextIntent del:
                return ApplyDelete(actions, del.Forward);
            case MoveCaretIntent mv:
                ApplyMove(actions, mv.Motion);
                return true;
            case FindNextIntent f:
                return ApplyFindNext(actions, f);
            default:
                return true;
        }
    }

    private static void ApplyConvertEol(IEditorActions actions, Eol target)
    {
        var text = actions.Text;
        var converted = LineEndings.ConvertTo(text, target);
        if (!string.Equals(converted, text, StringComparison.Ordinal))
            actions.Replace(0, text.Length, converted);
    }

    private static bool ApplyDelete(IEditorActions actions, bool forward)
    {
        var (start, length) = actions.Selection;
        if (length > 0)
        {
            actions.Replace(start, length, ""); // delete the selection
            return true;
        }

        var text = actions.Text;
        if (forward)
        {
            if (start >= text.Length)
                return false; // nothing ahead
            actions.Replace(start, 1, "");
        }
        else
        {
            if (start <= 0)
                return false; // backspace at the start
            actions.Replace(start - 1, 1, "");
        }

        return true;
    }

    private static void ApplyMove(IEditorActions actions, CaretMotion motion)
    {
        var text = actions.Text;
        var (start, length) = actions.Selection;
        var caret = Math.Clamp(start + length, 0, text.Length); // the active end of the selection
        actions.SetSelection(CaretTarget(text, caret, motion), 0);
    }

    private static bool ApplyFindNext(IEditorActions actions, FindNextIntent f)
    {
        var text = actions.Text;
        var (start, length) = actions.Selection;
        var from = start + length;
        var outcome = TextSearch.FindAll(text, f.Pattern, f.CaseSensitive, f.Regex);
        foreach (var m in outcome.Matches)
        {
            if (m.Offset >= from)
            {
                actions.SetSelection(m.Offset, m.Length);
                return true;
            }
        }

        return false; // no match at/after the caret — EOF (no wrap), which stops until-no-match replay
    }

    private static int CaretTarget(string text, int caret, CaretMotion motion) => motion switch
    {
        CaretMotion.Left => Math.Max(0, caret - 1),
        CaretMotion.Right => Math.Min(text.Length, caret + 1),
        CaretMotion.LineStart => LineStartOffset(text, caret),
        CaretMotion.LineEnd => LineEndOffset(text, caret),
        CaretMotion.DocStart => 0,
        CaretMotion.DocEnd => text.Length,
        CaretMotion.WordLeft => WordLeftOffset(text, caret),
        CaretMotion.WordRight => WordRightOffset(text, caret),
        CaretMotion.Up => VerticalOffset(text, caret, up: true),
        CaretMotion.Down => VerticalOffset(text, caret, up: false),
        _ => caret,
    };

    private static int LineStartOffset(string text, int caret)
        => caret <= 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;

    private static int LineEndOffset(string text, int caret)
    {
        var nl = caret < text.Length ? text.IndexOf('\n', caret) : -1;
        return nl < 0 ? text.Length : nl;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static int WordRightOffset(string text, int i)
    {
        while (i < text.Length && !IsWordChar(text[i])) i++; // skip non-word
        while (i < text.Length && IsWordChar(text[i])) i++;  // skip the word
        return i;
    }

    private static int WordLeftOffset(string text, int i)
    {
        while (i > 0 && !IsWordChar(text[i - 1])) i--; // skip non-word
        while (i > 0 && IsWordChar(text[i - 1])) i--;  // skip the word
        return i;
    }

    private static int VerticalOffset(string text, int caret, bool up)
    {
        var lineStart = LineStartOffset(text, caret);
        var column = caret - lineStart;
        if (up)
        {
            if (lineStart == 0)
                return caret; // already on the first line
            var prevStart = LineStartOffset(text, lineStart - 1);
            var prevLen = (lineStart - 1) - prevStart; // chars before the '\n' ending the previous line
            return prevStart + Math.Min(column, prevLen);
        }

        var lineEnd = LineEndOffset(text, caret);
        if (lineEnd >= text.Length)
            return caret; // already on the last line
        var nextStart = lineEnd + 1;
        var nextLen = LineEndOffset(text, nextStart) - nextStart;
        return nextStart + Math.Min(column, nextLen);
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
