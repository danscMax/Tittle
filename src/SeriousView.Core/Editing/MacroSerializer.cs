using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeriousView.Core.Text;

namespace SeriousView.Core.Editing;

// Flat persisted shapes — deliberately NOT polymorphic. Each step is a tagged record (Op + a few
// optional fields); the deserializer rebuilds an intent ONLY for a known Op (the allowlist), so a saved
// or shared macro file can never carry anything but editor operations.
public sealed class MacroFileDto
{
    public int Version { get; set; } = 1;
    public List<MacroDto> Macros { get; set; } = new();
}

public sealed class MacroDto
{
    public string Name { get; set; } = "";
    public string Mode { get; set; } = nameof(RepeatMode.Once);
    public int Count { get; set; } = 1;
    public List<MacroStepDto> Steps { get; set; } = new();
}

public sealed class MacroStepDto
{
    public string Op { get; set; } = "";
    public string? Text { get; set; }
    public bool? Forward { get; set; }
    public string? LineOp { get; set; }
    public string? Eol { get; set; }
    public string? Motion { get; set; }
    public string? Pattern { get; set; }
    public bool? Regex { get; set; }
    public bool? CaseSensitive { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MacroFileDto))]
public sealed partial class MacroJsonContext : JsonSerializerContext;

/// <summary>Serializes saved macros to / from JSON. The deserializer enforces an <b>allowlist</b>: only
/// the known editor-operation ops rebuild an intent; an unknown op (a tampered or newer file) is dropped,
/// so a macro file is no more dangerous than a text snippet. Corrupt JSON yields an empty list, never a
/// crash. UI-free and testable.</summary>
public static class MacroSerializer
{
    public static string Serialize(IReadOnlyList<Macro> macros)
    {
        var dto = new MacroFileDto { Macros = macros.Select(ToDto).ToList() };
        return JsonSerializer.Serialize(dto, MacroJsonContext.Default.MacroFileDto);
    }

    public static IReadOnlyList<Macro> Deserialize(string json)
    {
        MacroFileDto? file;
        try
        {
            file = JsonSerializer.Deserialize(json, MacroJsonContext.Default.MacroFileDto);
        }
        catch (JsonException)
        {
            return Array.Empty<Macro>(); // corrupt file → no macros, never a crash
        }

        if (file?.Macros is null)
            return Array.Empty<Macro>();

        var result = new List<Macro>();
        foreach (var m in file.Macros)
        {
            var steps = new List<IEditorIntent>();
            foreach (var s in m.Steps)
                if (FromDto(s) is { } intent) // allowlist: an unknown op produces null → skipped
                    steps.Add(intent);

            if (steps.Count > 0 && Enum.TryParse<RepeatMode>(m.Mode, out var mode))
                result.Add(new Macro(m.Name, mode, m.Count, steps));
        }

        return result;
    }

    private static MacroDto ToDto(Macro m) => new()
    {
        Name = m.Name,
        Mode = m.Mode.ToString(),
        Count = m.Count,
        Steps = m.Steps.Select(ToStepDto).ToList(),
    };

    private static MacroStepDto ToStepDto(IEditorIntent intent) => intent switch
    {
        TransformLinesIntent t => new() { Op = "transformLines", LineOp = t.Op.ToString() },
        ConvertEolIntent c => new() { Op = "convertEol", Eol = c.Target.ToString() },
        InsertTextIntent i => new() { Op = "insertText", Text = i.Text },
        DeleteTextIntent d => new() { Op = "deleteText", Forward = d.Forward },
        MoveCaretIntent mv => new() { Op = "moveCaret", Motion = mv.Motion.ToString() },
        FindNextIntent f => new() { Op = "findNext", Pattern = f.Pattern, Regex = f.Regex, CaseSensitive = f.CaseSensitive },
        ReplaceSelectionIntent r => new() { Op = "replaceSelection", Text = r.Text, Pattern = r.Pattern, Regex = r.Regex, CaseSensitive = r.CaseSensitive },
        _ => new() { Op = "unknown" },
    };

    // THE ALLOWLIST. Only these ops yield an intent; anything else returns null and is dropped on load.
    private static IEditorIntent? FromDto(MacroStepDto s) => s.Op switch
    {
        "transformLines" when Enum.TryParse<LineOp>(s.LineOp, out var op) => new TransformLinesIntent(op),
        "convertEol" when Enum.TryParse<Eol>(s.Eol, out var eol) => new ConvertEolIntent(eol),
        "insertText" => new InsertTextIntent(s.Text ?? ""),
        "deleteText" => new DeleteTextIntent(s.Forward ?? false),
        "moveCaret" when Enum.TryParse<CaretMotion>(s.Motion, out var motion) => new MoveCaretIntent(motion),
        "findNext" => new FindNextIntent(s.Pattern ?? "", s.Regex ?? false, s.CaseSensitive ?? false),
        "replaceSelection" => new ReplaceSelectionIntent(s.Text ?? "", s.Pattern, s.Regex ?? false, s.CaseSensitive ?? false),
        _ => null,
    };
}
