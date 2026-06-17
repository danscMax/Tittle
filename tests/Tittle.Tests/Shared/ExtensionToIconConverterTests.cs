using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Tittle.Shared;
using Xunit;

namespace Tittle.Tests.Shared;

public class ExtensionToIconConverterTests
{
    private static object? Convert(ExtensionToIconConverter converter, string? ext)
        => converter.Convert(ext, typeof(Geometry), null, CultureInfo.InvariantCulture);

    [AvaloniaFact]
    public void RepeatedConvert_SameExtension_ReturnsSameCachedGeometry()
    {
        var converter = new ExtensionToIconConverter();

        var first = Convert(converter, ".cs");
        var second = Convert(converter, ".cs");

        Assert.NotNull(first);
        Assert.IsAssignableFrom<Geometry>(first);
        // Memoized: the exact same instance is handed back, not a fresh resource probe.
        Assert.Same(first, second);
    }

    [AvaloniaFact]
    public void CaseAndDotInsensitive_HitTheSameCacheEntry()
    {
        var converter = new ExtensionToIconConverter();

        var lower = Convert(converter, "cs");
        var dotted = Convert(converter, ".cs");
        var upper = Convert(converter, ".CS");

        Assert.NotNull(lower);
        Assert.Same(lower, dotted);
        Assert.Same(lower, upper);
    }

    [AvaloniaFact]
    public void SynonymousExtensions_ResolveToTheSameIcon()
    {
        var converter = new ExtensionToIconConverter();

        // Both map to "IconCode" → the same Geometry resource.
        Assert.Same(Convert(converter, ".cs"), Convert(converter, ".ts"));
        // Both map to "IconImage".
        Assert.Same(Convert(converter, ".png"), Convert(converter, ".jpg"));
    }

    [AvaloniaFact]
    public void UnknownAndEmpty_FallBackToDocument_DistinctFromCode()
    {
        var converter = new ExtensionToIconConverter();

        var unknown = Convert(converter, ".zzz");
        var empty = Convert(converter, "");
        var nullExt = Convert(converter, null);
        var code = Convert(converter, ".cs");

        Assert.NotNull(unknown);
        Assert.Same(unknown, empty);
        Assert.Same(unknown, nullExt);
        Assert.NotSame(unknown, code);
    }
}
