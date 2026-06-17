using System;
using Tittle.Core.Editing;
using Xunit;

namespace Tittle.Tests.Core;

public class MacroReplayEngineTests
{
    private sealed record Step : IEditorIntent; // a stand-in intent; the engine is dispatcher-agnostic

    private static Macro Make(RepeatMode mode, int count, int steps = 1)
    {
        var list = new IEditorIntent[steps];
        for (var i = 0; i < steps; i++)
            list[i] = new Step();
        return new Macro("m", mode, count, list);
    }

    [Fact]
    public void Once_RunsEveryStepExactlyOnce()
    {
        var calls = 0;
        MacroReplayEngine.Replay(Make(RepeatMode.Once, 1, steps: 2), _ => { calls++; return true; });
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Times_RunsThePassNTimes()
    {
        var calls = 0;
        MacroReplayEngine.Replay(Make(RepeatMode.Times, 3), _ => { calls++; return true; });
        Assert.Equal(3, calls);
    }

    [Fact]
    public void Times_StopsEarly_WhenAStepMakesNoProgress()
    {
        var calls = 0;
        MacroReplayEngine.Replay(Make(RepeatMode.Times, 5), _ => { calls++; return false; });
        Assert.Equal(1, calls); // the first pass failed → no further repeats
    }

    [Fact]
    public void UntilNoMatch_RepeatsUntilAStepReturnsFalse()
    {
        var passes = 0;
        MacroReplayEngine.Replay(Make(RepeatMode.UntilNoMatch, 0), _ => { passes++; return passes <= 3; });
        Assert.Equal(4, passes); // three progressing passes, then one that found nothing (stops)
    }

    [Fact]
    public void UntilNoMatch_HardCap_PreventsAnInfiniteLoop()
    {
        var calls = 0;
        MacroReplayEngine.Replay(Make(RepeatMode.UntilNoMatch, 0), _ => { calls++; return true; }, hardCap: 50);
        Assert.Equal(50, calls);
    }

    [Fact]
    public void EmptyMacro_DoesNothing()
    {
        var calls = 0;
        MacroReplayEngine.Replay(new Macro("m", RepeatMode.Once, 1, Array.Empty<IEditorIntent>()),
            _ => { calls++; return true; });
        Assert.Equal(0, calls);
    }
}
