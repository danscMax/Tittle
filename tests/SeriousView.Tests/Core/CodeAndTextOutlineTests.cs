using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class SymbolOutlineTests
{
    [Fact]
    public void CSharp_ClassesAndMethods()
    {
        const string code = """
            public sealed class Loader
            {
                public async Task<int> LoadAsync(string path)
                {
                }

                private void Reset() { }
            }
            """;

        var outline = SymbolOutline.Parse(code, ".cs");

        Assert.Equal(new[] { "Loader", "LoadAsync", "Reset" }, outline.Select(h => h.Text));
        Assert.Equal(1, outline[0].Level);
        Assert.True(outline[1].Level > outline[0].Level); // indented members nest
        Assert.Equal(1, outline[0].Line);
        Assert.Equal(3, outline[1].Line);
    }

    [Fact]
    public void Python_DefsAndClasses_NestByIndent()
    {
        const string code = """
            class Parser:
                def parse(self):
                    pass

            def helper():
                pass
            """;

        var outline = SymbolOutline.Parse(code, ".py");

        Assert.Equal(new[] { "Parser", "parse", "helper" }, outline.Select(h => h.Text));
        Assert.Equal(new[] { 1, 2, 1 }, outline.Select(h => h.Level));
    }

    [Fact]
    public void JavaScript_FunctionsArrowsAndClasses()
    {
        const string code = """
            export class Store {}
            function plain(a) {}
            export const arrow = async (x) => x;
            """;

        var outline = SymbolOutline.Parse(code, ".ts");

        Assert.Equal(new[] { "Store", "plain", "arrow" }, outline.Select(h => h.Text));
    }

    [Fact]
    public void GoAndRust_Functions()
    {
        Assert.Equal(new[] { "Render" },
            SymbolOutline.Parse("func (v *View) Render() error {", ".go").Select(h => h.Text));
        Assert.Equal(new[] { "Anchor", "from_line" },
            SymbolOutline.Parse("pub struct Anchor {}\npub fn from_line(l: u32) {}", ".rs").Select(h => h.Text));
    }

    [Fact]
    public void UnsupportedExtension_ReturnsEmpty()
        => Assert.Empty(SymbolOutline.Parse("class X {}", ".bin"));

    [Fact]
    public void OrdinalsAreSequential()
    {
        var outline = SymbolOutline.Parse("def a():\n    pass\ndef b():\n    pass", ".py");

        Assert.Equal(new[] { 0, 1 }, outline.Select(h => h.Ordinal));
    }
}

public class TextOutlineTests
{
    [Fact]
    public void DecoratedAndHashHeadings()
    {
        const string text = """
            ==== Введение ====
            body
            ## Раздел два
            text
            """;

        var outline = TextOutline.Parse(text);

        Assert.Equal(new[] { "Введение", "Раздел два" }, outline.Select(h => h.Text));
        Assert.Equal(2, outline[1].Level);
    }

    [Fact]
    public void ChapterAndAllCapsHeadings()
    {
        const string text = """
            Глава 1. Старт
            обычный текст в строке
            ВАЖНОЕ ОБЪЯВЛЕНИЕ КОМАНДЫ
            ещё текст
            Chapter 2
            """;

        var outline = TextOutline.Parse(text);

        Assert.Equal(new[] { "Глава 1. Старт", "ВАЖНОЕ ОБЪЯВЛЕНИЕ КОМАНДЫ", "Chapter 2" },
            outline.Select(h => h.Text));
    }

    [Fact]
    public void AllCaps_RejectsShoutingSentencesAndCode()
    {
        var outline = TextOutline.Parse("ОДНОСЛОВО\nDO NOT PANIC!\nSELECT name FROM users WHERE id = 1 AND x = 2 OR y = 3 AND z = 4 OR w = 5");

        Assert.Empty(outline); // 1 word / trailing '!' / >10 words — none qualify
    }

    [Fact]
    public void CapsAt500Headings()
    {
        var lines = string.Join("\n", Enumerable.Range(1, 600).Select(i => $"# H{i}"));

        Assert.Equal(500, TextOutline.Parse(lines).Count);
    }
}
