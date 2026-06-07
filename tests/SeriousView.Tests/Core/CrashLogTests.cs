using System;
using SeriousView.Core.Diagnostics;
using Xunit;

namespace SeriousView.Tests.Core;

public class CrashLogTests
{
    [Fact]
    public void Format_IncludesTimestampSourceTypeMessageAndStack()
    {
        var when = new DateTimeOffset(2026, 6, 7, 13, 45, 0, TimeSpan.Zero);
        Exception ex;
        try { throw new InvalidOperationException("boom"); }
        catch (Exception e) { ex = e; }

        var entry = CrashLog.Format(when, ex, "AppDomain");

        Assert.Contains("2026-06-07 13:45:00 UTC", entry);
        Assert.Contains("AppDomain", entry);
        Assert.Contains("System.InvalidOperationException", entry);
        Assert.Contains("boom", entry);
        Assert.Contains("CrashLogTests", entry);   // stack trace names the throwing method's type
    }

    [Fact]
    public void Format_IncludesInnerExceptionChain()
    {
        var ex = new Exception("outer", new ArgumentNullException("param"));

        var entry = CrashLog.Format(DateTimeOffset.UnixEpoch, ex, "TaskScheduler");

        Assert.Contains("inner", entry);
        Assert.Contains("System.ArgumentNullException", entry);
    }
}
