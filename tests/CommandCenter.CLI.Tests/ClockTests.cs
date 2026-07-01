using System;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class ClockTests
{
    [Fact]
    public void SystemClock_ReturnsAUtcInstantNearNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset value = new SystemClock().UtcNow;
        Assert.True(value >= before.AddSeconds(-5) && value <= DateTimeOffset.UtcNow.AddSeconds(5));
    }
}
