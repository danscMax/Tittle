using SeriousView.Core.Abstractions;
using SeriousView.Core.Services;
using SeriousView.Core.Settings;
using Xunit;

namespace SeriousView.Tests.Core;

public class AppSettingsServiceTests
{
    [Fact]
    public void Current_DefaultsToDarkTheme_WhenStoreEmpty()
    {
        var svc = new AppSettingsService(new FakeSettingsStore());

        Assert.Equal(ThemeMode.Dark, svc.Current.Theme);
        Assert.Null(svc.Current.Window);
        Assert.Null(svc.Current.Session);
    }

    [Fact]
    public void Update_PersistsAndExposesNewValue()
    {
        var store = new FakeSettingsStore();
        var svc = new AppSettingsService(store);

        svc.Update(svc.Current with { Theme = ThemeMode.Light });

        Assert.Equal(ThemeMode.Light, svc.Current.Theme);
        Assert.Equal(1, store.SaveCount);
        // A fresh holder over the same store sees the persisted value.
        Assert.Equal(ThemeMode.Light, new AppSettingsService(store).Current.Theme);
    }

    [Fact]
    public void Update_DoesNotClobberOtherFields()
    {
        var svc = new AppSettingsService(new FakeSettingsStore());

        svc.Update(svc.Current with { Window = new WindowPlacement(800, 600, 10, 20, false) });
        svc.Update(svc.Current with { Theme = ThemeMode.Auto });

        Assert.Equal(ThemeMode.Auto, svc.Current.Theme);
        Assert.NotNull(svc.Current.Window);              // window survived the later theme update
        Assert.Equal(800, svc.Current.Window!.Width);
    }
}
