using Xunit;

namespace SeriousView.Tests;

/// <summary>
/// Sanity check that the build + test pipeline is wired up.
/// Real unit/Headless tests arrive in C6.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void Pipeline_IsAlive() => Assert.True(true);
}
