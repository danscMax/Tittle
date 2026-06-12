using SeriousView.Features.Viewer;
using Xunit;

namespace SeriousView.Tests.Features;

/// <summary>Guards P9: the per-line indent-column memo is FIFO-capped, so scrolling a huge
/// never-edited document (one stable document version) can't grow it without limit.</summary>
public class IndentGuideRendererTests
{
    [Fact]
    public void EffectiveColumns_Cache_StaysBoundedAcrossManyLines()
    {
        var renderer = new IndentGuideRenderer();
        string? LineAt(int n) => new string(' ', (n % 6) * 2) + "code();";

        // 20k distinct lines at a single (null = stable) version — the memo must not keep them all.
        for (var n = 1; n <= 20_000; n++)
            renderer.EffectiveColumns(LineAt, n, tabSize: 4, version: null);

        Assert.InRange(renderer.CacheCount, 1, 4096);
    }

    [Fact]
    public void EffectiveColumns_RepeatLine_IsCached()
    {
        var renderer = new IndentGuideRenderer();
        string? LineAt(int n) => "    indented";

        var first = renderer.EffectiveColumns(LineAt, 1, tabSize: 4, version: null);
        var before = renderer.CacheCount;
        var second = renderer.EffectiveColumns(LineAt, 1, tabSize: 4, version: null);

        Assert.Equal(first, second);
        Assert.Equal(before, renderer.CacheCount); // a re-hit adds no entry
    }
}
