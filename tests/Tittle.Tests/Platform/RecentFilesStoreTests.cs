using System;
using System.Collections.Generic;
using System.IO;
using Tittle.Platform;
using Xunit;

namespace Tittle.Tests.Platform;

public class RecentFilesStoreTests
{
    // A temp-root the GetTempFileName() scratch files do NOT live under, so only the File.Exists
    // filter applies in the "missing files" tests (the temp-folder filter is exercised separately).
    private static string NonMatchingTempRoot() =>
        Path.Combine(Path.GetTempPath(), "sv-recent-nonmatch-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Ctor_DropsMissingFiles_AndPrunesPersistence()
    {
        var real = Path.GetTempFileName();
        try
        {
            var missing = real + ".gone"; // never created
            var store = new FakeSettingsStore();
            store.Save("recent", new List<string> { missing, real });

            var recent = new RecentFilesStore(store, tempRoot: NonMatchingTempRoot());

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

            _ = new RecentFilesStore(store, tempRoot: NonMatchingTempRoot());

            Assert.Equal(before, store.SaveCount); // nothing to prune → no extra write
        }
        finally
        {
            File.Delete(real);
        }
    }

    [Fact]
    public void Ctor_DropsExistingFilesUnderTempRoot_AndPrunesPersistence()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "sv-temp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var underTemp = Path.Combine(tempRoot, "mgs.cs"); // exists, but lives under temp
        File.WriteAllText(underTemp, "x");
        var keep = Path.GetTempFileName();                // exists, NOT under tempRoot subdir
        try
        {
            var store = new FakeSettingsStore();
            store.Save("recent", new List<string> { underTemp, keep });

            var recent = new RecentFilesStore(store, tempRoot: tempRoot);

            Assert.Equal(new[] { keep }, recent.Items);                       // temp-located path dropped
            Assert.Equal(new[] { keep }, store.Load<List<string>>("recent")); // persistence pruned
        }
        finally
        {
            File.Delete(keep);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Add_FileUnderTempRoot_IsNotRecorded()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "sv-temp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var underTemp = Path.Combine(tempRoot, "mgr.cs");
        File.WriteAllText(underTemp, "x");
        try
        {
            var store = new FakeSettingsStore();
            var recent = new RecentFilesStore(store, tempRoot: tempRoot);
            var before = store.SaveCount;

            recent.Add(underTemp);

            Assert.Empty(recent.Items);
            Assert.Equal(before, store.SaveCount); // not recorded → no write
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }
}
