using System;
using System.Collections.Generic;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SeriousView.Core.Text;

namespace SeriousView.Features.Viewer;

/// <summary>Paints cv-* token foregrounds (timestamps, ips, TODOs, units…) over the TextMate
/// highlighting in non-markdown source views. Token scanning is pure Core (CodeDecorations);
/// this transformer only maps kinds to themed brushes supplied by the view.</summary>
public sealed class CodeDecorationColorizer : DocumentColorizingTransformer
{
    private IReadOnlyDictionary<CodeDecorationKind, IBrush> _palette =
        new Dictionary<CodeDecorationKind, IBrush>();

    /// <summary>Today's date for the relative-date tooltips/scans; injectable for tests.</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Swaps the themed brushes (called by the view on load and on theme change).</summary>
    public void SetPalette(IReadOnlyDictionary<CodeDecorationKind, IBrush> palette) => _palette = palette;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length == 0 || line.Length > CodeDecorations.MaxLineLength || _palette.Count == 0)
            return;

        var text = CurrentContext.Document.GetText(line);
        foreach (var d in CodeDecorations.ScanLine(text, Today()))
        {
            if (!_palette.TryGetValue(d.Kind, out var brush))
                continue;

            var bold = d.Kind is CodeDecorationKind.Todo or CodeDecorationKind.LogLevel;
            ChangeLinePart(line.Offset + d.Start, line.Offset + d.Start + d.Length, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
                if (bold)
                {
                    var tf = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(tf.FontFamily, tf.Style, FontWeight.Bold));
                }
            });
        }
    }
}
