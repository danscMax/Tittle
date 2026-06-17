using System.IO;
using Tittle.Core.Editing;
using Tittle.Platform;
using Xunit;

namespace Tittle.Tests.Features;

public class MacroStoreTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsTheLibrary()
    {
        var dir = Directory.CreateTempSubdirectory("sv-macros");
        try
        {
            var store = new MacroStore(Path.Combine(dir.FullName, "macros.json"));
            var macro = new Macro("Макрос 1", RepeatMode.UntilNoMatch, 1, new IEditorIntent[]
            {
                new FindNextIntent("a", Regex: false, CaseSensitive: false),
                new ReplaceSelectionIntent("b"),
            });

            store.Save(new[] { macro });
            var loaded = store.Load();

            Assert.Single(loaded);
            Assert.Equal(macro.Name, loaded[0].Name);
            Assert.Equal(macro.Mode, loaded[0].Mode);
            Assert.Equal(macro.Steps, loaded[0].Steps);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        var dir = Directory.CreateTempSubdirectory("sv-macros");
        try
        {
            var store = new MacroStore(Path.Combine(dir.FullName, "nope.json"));
            Assert.Empty(store.Load());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
