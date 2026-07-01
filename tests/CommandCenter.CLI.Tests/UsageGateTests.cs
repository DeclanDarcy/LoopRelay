using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class UsageGateTests
{
    private static (UsageGate Gate, FakeCodexUsageProbe Probe, FakeUsageDelay Delay, RecordingLoopConsole Con) New()
    {
        var probe = new FakeCodexUsageProbe();
        var delay = new FakeUsageDelay();
        var con = new RecordingLoopConsole();
        return (new UsageGate(probe, delay, con), probe, delay, con);
    }

    [Fact]
    public async Task WaitForCapacity_WhenBothLimitsHaveCapacity_DoesNotDelay()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(50, TimeSpan.FromHours(1), 60, TimeSpan.FromHours(2));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Empty(t.Delay.Delays);
    }

    [Fact]
    public async Task WaitForCapacity_WhenFiveHourExhausted_DelaysUntilTheFiveHourReset()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(0, TimeSpan.FromMinutes(30), 60, TimeSpan.FromHours(2));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromMinutes(30) }, t.Delay.Delays);
        Assert.Contains(t.Con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task WaitForCapacity_WhenWeeklyExhausted_DelaysUntilTheWeeklyReset()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(40, TimeSpan.FromHours(1), 0, TimeSpan.FromHours(3));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromHours(3) }, t.Delay.Delays);
    }

    [Fact]
    public async Task WaitForCapacity_WhenBothExhausted_DelaysUntilTheLaterReset()
    {
        var t = New();
        // 5h resets in 1h, weekly in 5h — you cannot work until BOTH have budget, so wait the later one.
        t.Probe.Default = new CodexUsageStatus(0, TimeSpan.FromHours(1), 0, TimeSpan.FromHours(5));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(new[] { TimeSpan.FromHours(5) }, t.Delay.Delays);
    }

    [Fact]
    public async Task WaitForCapacity_WhenProbeReturnsNull_FailsOpenWithoutDelaying()
    {
        var t = New();
        t.Probe.Default = null;

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Empty(t.Delay.Delays);
        Assert.Contains(t.Con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task WaitForCapacity_WhenExhaustedButResetAlreadyElapsed_DoesNotDelay()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(0, TimeSpan.FromMinutes(-5), 50, TimeSpan.FromHours(1));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Empty(t.Delay.Delays);
    }

    private static System.Collections.Generic.IEnumerable<string> Warnings(RecordingLoopConsole con) =>
        con.Events.Where(e => e.Kind == "warn").Select(e => e.Text);

    [Fact]
    public async Task WaitForCapacity_WhenFiveHourExhausted_WarnsWhichLimitAndHowLong()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(0, TimeSpan.FromMinutes(90), 50, TimeSpan.FromHours(2));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Contains(Warnings(t.Con), w => w.Contains("5h") && w.Contains("1h 30m"));
    }

    [Fact]
    public async Task WaitForCapacity_WhenWeeklyExhausted_WarnsThatItIsTheWeeklyLimit()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(40, TimeSpan.FromHours(1), 0, TimeSpan.FromHours(3));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Contains(Warnings(t.Con), w => w.Contains("weekly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WaitForCapacity_RendersMultiDayResetDurationsWithADaysComponent()
    {
        var t = New();
        var reset = new TimeSpan(2, 3, 4, 0); // 2 days, 3 hours, 4 minutes
        t.Probe.Default = new CodexUsageStatus(50, TimeSpan.FromHours(1), 0, reset);

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Contains(Warnings(t.Con), w => w.Contains("2d 3h 4m"));
    }

    [Fact]
    public async Task WaitForCapacity_QueriesTheProbeExactlyOnce()
    {
        var t = New();
        t.Probe.Default = new CodexUsageStatus(50, TimeSpan.FromHours(1), 60, TimeSpan.FromHours(2));

        await t.Gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.Equal(1, t.Probe.Calls);
    }
}
