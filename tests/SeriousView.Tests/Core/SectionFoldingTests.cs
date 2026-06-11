using System.Linq;
using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class SectionFoldingTests
{
    private static HeadingOutline H(int level, int line, int ordinal)
        => new($"h{ordinal}", level, line, ordinal);

    [Fact]
    public void Compute_FlatHeadings_SectionsRunToTheNextHeading()
    {
        var outline = new[] { H(1, 1, 0), H(1, 5, 1) };

        var sections = SectionFolding.Compute(outline, lineCount: 10);

        Assert.Equal(2, sections.Count);
        Assert.Equal((1, 4), (sections[0].HeadingLine, sections[0].EndLine));
        Assert.Equal((5, 10), (sections[1].HeadingLine, sections[1].EndLine));
    }

    [Fact]
    public void Compute_NestedHeading_StaysInsideItsParent()
    {
        // h1(level1) at 1, h2(level2) at 3, h1(level1) at 6 — the level-2 section ends
        // before the next level-1; the first level-1 spans across its child.
        var outline = new[] { H(1, 1, 0), H(2, 3, 1), H(1, 6, 2) };

        var sections = SectionFolding.Compute(outline, lineCount: 8);

        Assert.Equal((1, 5), (sections[0].HeadingLine, sections[0].EndLine));
        Assert.Equal((3, 5), (sections[1].HeadingLine, sections[1].EndLine));
        Assert.Equal((6, 8), (sections[2].HeadingLine, sections[2].EndLine));
    }

    [Fact]
    public void Compute_EmptySection_IsSkipped()
    {
        // Two adjacent headings — the first has no body to fold.
        var outline = new[] { H(1, 1, 0), H(1, 2, 1) };

        var sections = SectionFolding.Compute(outline, lineCount: 4);

        Assert.Single(sections);
        Assert.Equal(2, sections[0].HeadingLine);
    }

    [Fact]
    public void Compute_NoHeadings_NoSections()
        => Assert.Empty(SectionFolding.Compute(System.Array.Empty<HeadingOutline>(), 5));
}
