using LoopRelay.Cli.Services;
using Xunit;

namespace LoopRelay.Cli.Tests.Services;

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
