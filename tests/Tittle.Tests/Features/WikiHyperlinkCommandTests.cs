using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Input;
using Tittle.Features.Viewer;
using Xunit;

namespace Tittle.Tests.Features;

public class WikiHyperlinkCommandTests : IDisposable
{
    private sealed class RecordingCommand : ICommand
    {
        public string? Executed;
        public bool CanExec = true;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => CanExec;

        public void Execute(object? parameter) => Executed = parameter as string;
    }

    private readonly string _root;
    private readonly List<string> _opened = [];
    private readonly RecordingCommand _fallback = new();

    public WikiHyperlinkCommandTests()
    {
        _root = Directory.CreateTempSubdirectory("sv-wiki-").FullName;
        File.WriteAllText(Path.Combine(_root, "note.md"), "# note");
        File.WriteAllText(Path.Combine(_root, "my note.md"), "# spaced");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private WikiHyperlinkCommand Create(bool withRoot = true)
        => new(() => withRoot ? _root : null, _opened.Add, _fallback);

    [Fact]
    public void Execute_WikiUrl_OpensTheSiblingNote()
    {
        Create().Execute("wiki:note");

        Assert.Equal(new[] { Path.Combine(_root, "note.md") }, _opened);
        Assert.Null(_fallback.Executed);
    }

    [Fact]
    public void Execute_EncodedName_Decodes()
    {
        Create().Execute("wiki:my%20note");

        Assert.Equal(new[] { Path.Combine(_root, "my note.md") }, _opened);
    }

    [Fact]
    public void Execute_MissingFile_DoesNothing()
    {
        Create().Execute("wiki:gone");

        Assert.Empty(_opened);
    }

    [Fact]
    public void Execute_WithoutAssetRoot_DoesNothing()
    {
        Create(withRoot: false).Execute("wiki:note");

        Assert.Empty(_opened);
    }

    [Theory]
    [InlineData("wiki:%2e%2e%2fpasswd")]
    [InlineData("wiki:..%5Cpasswd")]
    [InlineData("wiki:a%2Fb")]
    [InlineData("wiki:C%3A%5Cevil")]
    public void Execute_TraversalOrRootedNames_DoNothing(string url)
    {
        Create().Execute(url);

        Assert.Empty(_opened);
        Assert.Null(_fallback.Executed); // wiki: URLs never reach the fallback either
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("file:///etc/passwd")]
    public void Execute_NonWiki_DelegatesVerbatimToTheFallback(string url)
    {
        Create().Execute(url);

        Assert.Equal(url, _fallback.Executed);
        Assert.Empty(_opened);
    }

    [Fact]
    public void CanExecute_Wiki_IsSyntacticPlusRootPresence()
    {
        Assert.True(Create().CanExecute("wiki:anything")); // no File.Exists per hover
        Assert.False(Create().CanExecute("wiki:a/b"));
        Assert.False(Create(withRoot: false).CanExecute("wiki:note"));
    }

    [Fact]
    public void CanExecute_NonWiki_DelegatesToTheFallback()
    {
        var cmd = Create();

        _fallback.CanExec = true;
        Assert.True(cmd.CanExecute("https://x.com"));
        _fallback.CanExec = false;
        Assert.False(cmd.CanExecute("https://x.com"));
    }
}
