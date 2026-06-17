using Tittle.Core.Editing;
using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class MacroSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesEveryIntentType()
    {
        var macro = new Macro("m", RepeatMode.UntilNoMatch, 3, new IEditorIntent[]
        {
            new InsertTextIntent("hi"),
            new MoveCaretIntent(CaretMotion.WordRight),
            new DeleteTextIntent(Forward: true),
            new FindNextIntent("x", Regex: true, CaseSensitive: false),
            new ReplaceSelectionIntent("y"),
            new ReplaceSelectionIntent("$2=$1", "(\\w+)=(\\w+)", Regex: true, CaseSensitive: true),
            new TransformLinesIntent(LineOp.SortAscending),
            new ConvertEolIntent(Eol.CrLf),
        }, Shortcut: "Ctrl+Shift+M");

        var back = MacroSerializer.Deserialize(MacroSerializer.Serialize(new[] { macro }));

        Assert.Single(back);
        Assert.Equal(macro.Name, back[0].Name);
        Assert.Equal(macro.Mode, back[0].Mode);
        Assert.Equal(macro.Count, back[0].Count);
        Assert.Equal(macro.Shortcut, back[0].Shortcut);
        Assert.Equal(macro.Steps, back[0].Steps); // element-wise; each intent is a value-equal record
    }

    [Fact]
    public void Deserialize_UnknownOp_IsDropped_NeverExecuted()
    {
        // A tampered/newer file with an op outside the allowlist must not produce any intent for it.
        var json = """
        { "Version":1, "Macros":[ { "Name":"m","Mode":"Once","Count":1,"Steps":[
            {"Op":"insertText","Text":"ok"},
            {"Op":"runShell","Text":"rm -rf /"},
            {"Op":"moveCaret","Motion":"Left"}
        ]}]}
        """;

        var back = MacroSerializer.Deserialize(json);

        Assert.Single(back);
        Assert.Equal(2, back[0].Steps.Count); // the "runShell" step was dropped
        Assert.IsType<InsertTextIntent>(back[0].Steps[0]);
        Assert.IsType<MoveCaretIntent>(back[0].Steps[1]);
    }

    [Fact]
    public void Deserialize_CorruptJson_ReturnsEmpty_NeverThrows()
        => Assert.Empty(MacroSerializer.Deserialize("}{ not json"));

    [Fact]
    public void Deserialize_MacroWithNoValidSteps_IsDropped()
    {
        var json = """{ "Version":1, "Macros":[ { "Name":"m","Mode":"Once","Count":1,"Steps":[ {"Op":"bogus"} ]}]}""";
        Assert.Empty(MacroSerializer.Deserialize(json));
    }
}
