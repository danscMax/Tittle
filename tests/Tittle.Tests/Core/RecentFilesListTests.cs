using System.Linq;
using Tittle.Core.Services;
using Xunit;

namespace Tittle.Tests.Core;

public class RecentFilesListTests
{
    [Fact]
    public void Add_PutsNewestFirst()
    {
        var list = new RecentFilesList();
        list.Add("a.txt");
        list.Add("b.txt");

        Assert.Equal(new[] { "b.txt", "a.txt" }, list.Paths.ToArray());
    }

    [Fact]
    public void Add_DeduplicatesCaseInsensitive_AndMovesToFront()
    {
        var list = new RecentFilesList();
        list.Add("a.txt");
        list.Add("b.txt");
        list.Add("A.TXT"); // same as a.txt

        Assert.Equal(new[] { "A.TXT", "b.txt" }, list.Paths.ToArray());
    }

    [Fact]
    public void Add_CapsLength()
    {
        var list = new RecentFilesList(cap: 3);
        for (var i = 0; i < 5; i++)
            list.Add($"f{i}.txt");

        Assert.Equal(3, list.Paths.Count);
        Assert.Equal(new[] { "f4.txt", "f3.txt", "f2.txt" }, list.Paths.ToArray());
    }

    [Fact]
    public void Ctor_RespectsCap_AndIgnoresBlanks()
    {
        var list = new RecentFilesList(new[] { "a", "", "b", "c", "d" }, cap: 2);

        Assert.Equal(new[] { "a", "b" }, list.Paths.ToArray());
    }
}
