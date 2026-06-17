using System;
using System.Collections.Generic;

namespace Tittle.Core.Editing;

/// <summary>Replays a <see cref="Macro"/> by re-dispatching its steps. Pure of UI and of the concrete
/// dispatcher: the caller passes a <paramref name="dispatch"/> delegate that applies one intent and
/// returns whether it made PROGRESS — a "find" step that no longer matches returns <c>false</c>, which
/// ends the <see cref="RepeatMode.UntilNoMatch"/> loop. The <c>hardCap</c> guards against a macro with
/// no advancing step spinning forever.</summary>
public static class MacroReplayEngine
{
    public const int DefaultHardCap = 100_000;

    public static void Replay(Macro macro, Func<IEditorIntent, bool> dispatch, int hardCap = DefaultHardCap)
    {
        if (macro.Steps.Count == 0)
            return;

        switch (macro.Mode)
        {
            case RepeatMode.Once:
                RunPass(macro.Steps, dispatch);
                break;

            case RepeatMode.Times:
                for (var i = 0; i < macro.Count; i++)
                    if (!RunPass(macro.Steps, dispatch))
                        break; // a step stopped making progress → no point repeating
                break;

            case RepeatMode.UntilNoMatch:
                for (var i = 0; i < hardCap; i++)
                    if (!RunPass(macro.Steps, dispatch))
                        break;
                break;
        }
    }

    // Run the steps once; stop and report false the moment a step makes no progress (e.g. a failed find),
    // abandoning the rest of the pass (there is nothing for the following steps to act on).
    private static bool RunPass(IReadOnlyList<IEditorIntent> steps, Func<IEditorIntent, bool> dispatch)
    {
        foreach (var step in steps)
            if (!dispatch(step))
                return false;
        return true;
    }
}
