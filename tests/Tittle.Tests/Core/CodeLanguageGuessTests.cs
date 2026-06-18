using Tittle.Core.Text;
using Xunit;

namespace Tittle.Tests.Core;

public class CodeLanguageGuessTests
{
    [Theory]
    [InlineData("{ \"name\": \"x\", \"n\": 1 }", "json")]
    [InlineData("[ { \"id\": 1 } ]", "json")]
    [InlineData("<?xml version=\"1.0\"?>\n<root/>", "xml")]
    [InlineData("<note>\n  <to>x</to>\n</note>", "xml")]
    [InlineData("#!/bin/bash\necho hi", "bash")]
    [InlineData("def greet(name):\n    return name", "python")]
    [InlineData("import os\nprint(os.name)", "python")]
    [InlineData("public class Foo {\n  public int Bar() { return 1; }\n}", "csharp")]
    [InlineData("using System;\nnamespace N {}", "csharp")]
    [InlineData("const x = 1;\nconsole.log(x)", "javascript")]
    [InlineData("function add(a, b) { return a + b; }", "javascript")]
    [InlineData("SELECT id, name FROM users WHERE id = 1;", "sql")]
    public void Guess_KnownSamples_ReturnExpectedId(string body, string expected)
        => Assert.Equal(expected, CodeLanguageGuess.Guess(body.Split('\n')));

    [Theory]
    [InlineData("just some plain prose without code markers")]
    [InlineData("")]
    [InlineData("12345\n67890")]
    public void Guess_Ambiguous_ReturnsNull(string body)
        => Assert.Null(CodeLanguageGuess.Guess(body.Split('\n')));

    [Fact]
    public void Guess_EmptyList_ReturnsNull()
        => Assert.Null(CodeLanguageGuess.Guess(System.Array.Empty<string>()));
}
