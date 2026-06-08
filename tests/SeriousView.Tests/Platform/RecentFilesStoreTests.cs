using System.Collections.Generic;
using System.IO;
using SeriousView.Platform;
using Xunit;

namespace SeriousView.Tests.Platform;

public class RecentFilesStoreTests
{
    [Fact]
    public void Ctor_DropsMissingFiles_AndPrunesPersistence()
    {
        var real = Path.GetTempFileName();
        try
        {
            var missing = real + ".gone"; // never created
            var store = new FakeSettingsStore();
            store.Save("recent", new List<string> { missing, real });

            var recent = new RecentFilesStore(store);

            Assert.Equal(new[] { real }, recent.Items);                          // dead path dropped
            Assert.Equal(new[] { real }, store.Load<List<string>>("recent"));    // persistence pruned
        }
        finally
        {
            File.Delete(real);
        }
    }

    [Fact]
    public void Ctor_AllExisting_DoesNotRewritePersistence()
    {
        var real = Path.GetTempFileName();
        try
        {
            var store = new FakeSettingsStore();
            store.Save("recent", new List<string> { real });
            var before = store.SaveCount;

            _ = new RecentFilesStore(store);

            Assert.Equal(before, store.SaveCount); // nothing to prune → no extra write
        }
        finally
        {
            File.Delete(real);
        }
    }
}
