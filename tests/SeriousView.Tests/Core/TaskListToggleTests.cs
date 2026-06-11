using SeriousView.Core.Text;
using Xunit;

namespace SeriousView.Tests.Core;

public class TaskListToggleTests
{
    [Fact]
    public void ToggleAt_ChecksAnUncheckedBox()
        => Assert.Equal("- [x] one\n- [ ] two",
            TaskListToggle.ToggleAt("- [ ] one\n- [ ] two", 0));

    [Fact]
    public void ToggleAt_UnchecksACheckedBox()
        => Assert.Equal("- [ ] one", TaskListToggle.ToggleAt("- [x] one", 0));

    [Fact]
    public void ToggleAt_SecondItem_LeavesTheFirstAlone()
        => Assert.Equal("- [ ] one\n- [x] two",
            TaskListToggle.ToggleAt("- [ ] one\n- [ ] two", 1));

    [Fact]
    public void ToggleAt_SkipsFencedLookalikes()
    {
        const string md = "```\n- [ ] code, not a task\n```\n- [ ] real";

        Assert.Equal("```\n- [ ] code, not a task\n```\n- [x] real",
            TaskListToggle.ToggleAt(md, 0));
    }

    [Fact]
    public void ToggleAt_IndexedItemsMatchThePreviewGlyphOrder()
    {
        // Indented/nested items count in document order, same as the glyph pass.
        const string md = "- [ ] a\n    - [x] nested\n- [ ] b";

        Assert.Equal("- [ ] a\n    - [ ] nested\n- [ ] b", TaskListToggle.ToggleAt(md, 1));
    }

    [Theory]
    [InlineData("- [ ] one", 5)]
    [InlineData("no tasks here", 0)]
    [InlineData("", 0)]
    public void ToggleAt_OutOfRangeOrNoTasks_ReturnsNull(string md, int index)
        => Assert.Null(TaskListToggle.ToggleAt(md, index));
}
