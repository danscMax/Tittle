using System.Collections.Generic;

namespace SeriousView.Core.Text;

/// <summary>Pure section-folding geometry (ported): each outline heading folds its body —
/// everything up to the next heading of the same or shallower level (or the document end).
/// Lines are 1-based to match <see cref="HeadingOutline.Line"/>.</summary>
public static class SectionFolding
{
    /// <summary>A foldable section: the heading stays visible, lines
    /// (<see cref="HeadingLine"/>, <see cref="EndLine"/>] collapse.</summary>
    public sealed record Section(int HeadingLine, int EndLine);

    public static IReadOnlyList<Section> Compute(IReadOnlyList<HeadingOutline> outline, int lineCount)
    {
        if (outline.Count == 0 || lineCount <= 0)
            return [];

        var sections = new List<Section>(outline.Count);
        for (var i = 0; i < outline.Count; i++)
        {
            var end = lineCount;
            for (var j = i + 1; j < outline.Count; j++)
            {
                if (outline[j].Level <= outline[i].Level)
                {
                    end = outline[j].Line - 1;
                    break;
                }
            }

            if (end > outline[i].Line)
                sections.Add(new Section(outline[i].Line, end));
        }

        return sections;
    }
}
