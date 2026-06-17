using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class LineOperationsTests
{
    [Fact]
    public void Sort_Ascending_CaseInsensitive_ByDefault()
        => Assert.Equal("Apple\nbanana\nCherry", LineOperations.Sort("banana\nCherry\nApple"));

    [Fact]
    public void Sort_Descending_Reverses()
        => Assert.Equal("Cherry\nbanana\nApple", LineOperations.Sort("banana\nCherry\nApple", descending: true));

    [Fact]
    public void Sort_CaseSensitive_OrdinalPutsUppercaseFirst()
        => Assert.Equal("Banana\napple", LineOperations.Sort("apple\nBanana", caseSensitive: true));

    [Fact]
    public void Sort_Cyrillic()
        => Assert.Equal("абрикос\nбанан\nвишня", LineOperations.Sort("вишня\nбанан\nабрикос"));

    [Fact]
    public void RemoveDuplicateLines_KeepsFirst_PreservesOrder()
        => Assert.Equal("b\na\nc", LineOperations.RemoveDuplicateLines("b\na\nb\nc\na"));

    [Fact]
    public void RemoveDuplicateLines_CaseSensitiveExactMatch()
        => Assert.Equal("a\nA", LineOperations.RemoveDuplicateLines("a\nA\na"));

    [Fact]
    public void TrimTrailing_StripsPerLine_KeepsLeading()
        => Assert.Equal("  a\nb", LineOperations.TrimTrailing("  a  \nb\t "));

    [Theory]
    [InlineData(CaseKind.Upper, "ПРИВЕТ WORLD", "привет world")]
    [InlineData(CaseKind.Lower, "привет world", "ПРИВЕТ World")]
    [InlineData(CaseKind.Title, "Привет Мир Foo-Bar", "привет мир foo-bar")]
    public void ChangeCase(CaseKind kind, string expected, string input)
        => Assert.Equal(expected, LineOperations.ChangeCase(input, kind));

    [Fact]
    public void MoveLines_Down_SwapsWithNext()
        => Assert.Equal("a\nc\nb\nd", LineOperations.MoveLines("a\nb\nc\nd", 1, 1, +1));

    [Fact]
    public void MoveLines_Up_SwapsWithPrevious()
        => Assert.Equal("a\nc\nb\nd", LineOperations.MoveLines("a\nb\nc\nd", 2, 2, -1));

    [Fact]
    public void MoveLines_Block_MovesTogether()
        => Assert.Equal("c\na\nb\nd", LineOperations.MoveLines("a\nb\nc\nd", 0, 1, +1));

    [Fact]
    public void MoveLines_PastEdge_IsNoOp()
    {
        Assert.Equal("a\nb", LineOperations.MoveLines("a\nb", 0, 0, -1)); // already at top
        Assert.Equal("a\nb", LineOperations.MoveLines("a\nb", 1, 1, +1)); // already at bottom
    }

    [Fact]
    public void DuplicateLines_InsertsCopyAfter()
        => Assert.Equal("a\nb\nb\nc", LineOperations.DuplicateLines("a\nb\nc", 1, 1));

    [Fact]
    public void DuplicateLines_Block()
        => Assert.Equal("a\nb\na\nb\nc", LineOperations.DuplicateLines("a\nb\nc", 0, 1));

    [Fact]
    public void JoinLines_SpaceSeparated_Trimmed()
        => Assert.Equal("a b c\nd", LineOperations.JoinLines("a \n  b\nc\nd", 0, 2));

    [Fact]
    public void JoinLines_SingleLine_IsNoOp()
        => Assert.Equal("a\nb", LineOperations.JoinLines("a\nb", 0, 0));

    [Fact]
    public void Operations_EmptyAndSingleLine_AreSafe()
    {
        Assert.Equal("", LineOperations.Sort(""));
        Assert.Equal("solo", LineOperations.RemoveDuplicateLines("solo"));
        Assert.Equal("solo", LineOperations.TrimTrailing("solo"));
        Assert.Equal("", LineOperations.MoveLines("", 0, 0, 1));
    }
}
