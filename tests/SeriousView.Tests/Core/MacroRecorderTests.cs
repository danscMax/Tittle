using SeriousView.Core.Editing;
using Xunit;

namespace SeriousView.Tests.Core;

public class MacroRecorderTests
{
    [Fact]
    public void Record_IsIgnored_WhenNotRecording()
    {
        var r = new MacroRecorder();
        r.Record(new MoveCaretIntent(CaretMotion.Left));
        Assert.Equal(0, r.StepCount);
    }

    [Fact]
    public void Start_BeginsRecording()
    {
        var r = new MacroRecorder();
        r.Start();
        r.Record(new MoveCaretIntent(CaretMotion.Left));
        Assert.True(r.IsRecording);
        Assert.Equal(1, r.StepCount);
    }

    [Fact]
    public void ConsecutiveInserts_AreCoalesced()
    {
        var r = new MacroRecorder();
        r.Start();
        r.Record(new InsertTextIntent("a"));
        r.Record(new InsertTextIntent("b"));
        r.Record(new InsertTextIntent("c"));

        var macro = r.Stop("m");

        Assert.NotNull(macro);
        Assert.Single(macro!.Steps);
        Assert.Equal("abc", ((InsertTextIntent)macro.Steps[0]).Text);
    }

    [Fact]
    public void Inserts_AreNotCoalesced_AcrossAnotherIntent()
    {
        var r = new MacroRecorder();
        r.Start();
        r.Record(new InsertTextIntent("a"));
        r.Record(new MoveCaretIntent(CaretMotion.Right));
        r.Record(new InsertTextIntent("b"));

        Assert.Equal(3, r.Stop("m")!.Steps.Count);
    }

    [Fact]
    public void Stop_WithNoSteps_ReturnsNull_AndEndsRecording()
    {
        var r = new MacroRecorder();
        r.Start();

        Assert.Null(r.Stop("m"));
        Assert.False(r.IsRecording);
    }

    [Fact]
    public void Stop_CarriesNameModeAndCount()
    {
        var r = new MacroRecorder();
        r.Start();
        r.Record(new MoveCaretIntent(CaretMotion.Down));

        var macro = r.Stop("trim", RepeatMode.UntilNoMatch, 5);

        Assert.Equal("trim", macro!.Name);
        Assert.Equal(RepeatMode.UntilNoMatch, macro.Mode);
        Assert.Equal(5, macro.Count);
    }
}
