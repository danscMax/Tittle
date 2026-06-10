using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Shared;

public class OrdinalMatchConverterTests
{
    private static object Convert(params object?[] values)
        => OrdinalMatchConverter.Instance.Convert(
            new List<object?>(values), typeof(bool), null, CultureInfo.InvariantCulture)!;

    [Fact]
    public void EqualOrdinals_True() => Assert.Equal(true, Convert(2, 2));

    [Fact]
    public void DifferentOrdinals_False() => Assert.Equal(false, Convert(1, 2));

    [Fact]
    public void UnsetValue_False() => Assert.Equal(false, Convert(AvaloniaProperty.UnsetValue, 1));

    [Fact]
    public void NonInt_OrNull_OrShortList_False()
    {
        Assert.Equal(false, Convert("1", 1));
        Assert.Equal(false, Convert(null, 1));
        Assert.Equal(false, Convert(3));
    }
}
