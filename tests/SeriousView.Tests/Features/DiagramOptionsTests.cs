using SeriousView.Core.Settings;
using SeriousView.Shared;
using Xunit;

namespace SeriousView.Tests.Features;

public class DiagramOptionsTests
{
    [Fact]
    public void FromSettings_Null_IsDisabledWithDefaultUrl()
    {
        var opts = DiagramOptions.FromSettings(null);
        Assert.False(opts.Enabled);
        Assert.Equal(DiagramOptions.DefaultKrokiUrl, opts.KrokiUrl);
    }

    [Fact]
    public void RoundTrip_PreservesEnabledAndUrl()
    {
        var opts = new DiagramOptions { Enabled = true, KrokiUrl = "http://localhost:8000" };
        var restored = DiagramOptions.FromSettings(opts.ToSettings());

        Assert.True(restored.Enabled);
        Assert.Equal("http://localhost:8000", restored.KrokiUrl);
    }

    [Fact]
    public void FromSettings_BlankUrl_FallsBackToDefault()
    {
        var restored = DiagramOptions.FromSettings(new DiagramSettings(Enabled: true, KrokiUrl: "  "));
        Assert.Equal(DiagramOptions.DefaultKrokiUrl, restored.KrokiUrl);
    }
}
