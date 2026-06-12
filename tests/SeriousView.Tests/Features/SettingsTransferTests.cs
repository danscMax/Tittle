using System.Collections.Generic;
using SeriousView.Core.Abstractions;
using SeriousView.Core.Settings;
using SeriousView.Features.Shell;
using Xunit;

namespace SeriousView.Tests.Features;

public class SettingsTransferTests
{
    [Fact]
    public void Serialize_StripsTheMachinePrivateSessionPaths()
    {
        var settings = new AppSettings
        {
            Session = new SessionState(new List<string> { @"C:\private\doc.md" }, 0),
            Window = new WindowPlacement(800, 600, 0, 0, false),
            Editor = new EditorSettings(16, false, true),
        };

        var json = SettingsTransfer.Serialize(settings);

        Assert.DoesNotContain("private", json); // the open-file path is never shared
        Assert.Contains("Editor", json);        // preferences are kept
    }

    [Fact]
    public void Parse_RoundTripsAValidSettingsFile()
    {
        var original = new AppSettings { Theme = ThemeMode.Light, Editor = new EditorSettings(18, true, false) };
        var json = SettingsTransfer.Serialize(original);

        var (status, parsed) = SettingsTransfer.Parse(json);

        Assert.Equal(SettingsTransfer.ParseStatus.Ok, status);
        Assert.Equal(ThemeMode.Light, parsed!.Theme);
        Assert.Equal(18, parsed.Editor!.FontSize);
    }

    [Fact]
    public void Parse_RoundTripsSplitLayoutFields()
    {
        var original = new AppSettings
        {
            Layout = new LayoutSettings { SplitOrientation = SplitOrientation.Vertical, SplitRatio = 0.7 },
        };
        var json = SettingsTransfer.Serialize(original);

        var (status, parsed) = SettingsTransfer.Parse(json);

        Assert.Equal(SettingsTransfer.ParseStatus.Ok, status);
        Assert.Equal(SplitOrientation.Vertical, parsed!.Layout!.SplitOrientation);
        Assert.Equal(0.7, parsed.Layout.SplitRatio);
    }

    [Theory]
    [InlineData("{}")]              // empty object — would reset preferences
    [InlineData("not json at all")] // garbage
    [InlineData("[1,2,3]")]         // valid JSON but not a settings object
    public void Parse_RejectsNonSettingsInput(string raw)
    {
        var (status, parsed) = SettingsTransfer.Parse(raw);

        Assert.Equal(SettingsTransfer.ParseStatus.NotSettings, status);
        Assert.Null(parsed);
    }
}
