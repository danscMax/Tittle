using Tittle.Core.Settings;
using Tittle.Shared;
using Xunit;

namespace Tittle.Tests.Shared;

public class EditorOptionsTests
{
    [Fact]
    public void ZoomIn_IsClampedToMax()
    {
        var o = new EditorOptions { FontSize = EditorOptions.MaxFontSize };
        o.ZoomIn();
        Assert.Equal(EditorOptions.MaxFontSize, o.FontSize);
    }

    [Fact]
    public void ZoomOut_IsClampedToMin()
    {
        var o = new EditorOptions { FontSize = EditorOptions.MinFontSize };
        o.ZoomOut();
        Assert.Equal(EditorOptions.MinFontSize, o.FontSize);
    }

    [Fact]
    public void ZoomIn_ThenOut_ReturnsToStart()
    {
        var o = new EditorOptions { FontSize = 14 };
        o.ZoomIn();
        Assert.Equal(15, o.FontSize);
        o.ZoomOut();
        Assert.Equal(14, o.FontSize);
    }

    [Fact]
    public void ResetZoom_RestoresDefault()
    {
        var o = new EditorOptions { FontSize = 22 };
        o.ResetZoom();
        Assert.Equal(EditorOptions.DefaultFontSize, o.FontSize);
    }

    [Fact]
    public void Toggles_FlipFlags()
    {
        var o = new EditorOptions();

        Assert.False(o.WordWrap);
        o.ToggleWordWrap();
        Assert.True(o.WordWrap);

        Assert.True(o.ShowLineNumbers);
        o.ToggleLineNumbers();
        Assert.False(o.ShowLineNumbers);
    }

    [Theory]
    [InlineData(14, 1.0)]   // default → 1:1
    [InlineData(28, 2.0)]   // double the default font → 2× preview scale
    [InlineData(7, 0.5)]    // half
    public void PreviewScale_TracksFontSizeRelativeToDefault(double fontSize, double expected)
    {
        var o = new EditorOptions { FontSize = fontSize };
        Assert.Equal(expected, o.PreviewScale, 3);
    }

    [Fact]
    public void PreviewScale_RaisesPropertyChanged_WhenFontSizeChanges()
    {
        var o = new EditorOptions { FontSize = 14 };
        var raised = false;
        o.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(EditorOptions.PreviewScale);

        o.ZoomIn();

        Assert.True(raised);
    }

    [Fact]
    public void FromSettings_Null_GivesDefaults()
    {
        var o = EditorOptions.FromSettings(null);

        Assert.Equal(EditorOptions.DefaultFontSize, o.FontSize);
        Assert.False(o.WordWrap);
        Assert.True(o.ShowLineNumbers);
    }

    [Fact]
    public void FromSettings_RoundTrips_AndClampsCorruptFont()
    {
        var o = EditorOptions.FromSettings(new EditorSettings(18, WordWrap: true, ShowLineNumbers: false));
        Assert.Equal(18, o.FontSize);
        Assert.True(o.WordWrap);
        Assert.False(o.ShowLineNumbers);

        // An out-of-range persisted font is clamped on load.
        Assert.Equal(EditorOptions.MaxFontSize,
            EditorOptions.FromSettings(new EditorSettings(999, false, true)).FontSize);

        var s = o.ToSettings();
        Assert.Equal(new EditorSettings(18, true, false), s);
    }
}
