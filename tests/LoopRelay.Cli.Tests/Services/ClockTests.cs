using LoopRelay.Cli;
using Xunit;

namespace LoopRelay.Cli.Tests;

public class ClockTests
{
    [Fact]
    public void SystemClock_ReturnsAUtcInstantNearNow()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        DateTimeOffset value = new Cli.SystemClock().UtcNow;
        Assert.True(value >= before.AddSeconds(-5) && value <= DateTimeOffset.UtcNow.AddSeconds(5));
    }
}
