using System;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class UsageGateCompositionTests
{
    [Fact]
    public async Task Create_WhenNotConfigured_IsANullGateThatNeverProbesOrWaits()
    {
        IUsageGate gate = UsageGateComposition.Create(new FakeCodexUsageProbe(), new FakeUsageDelay(), new RecordingLoopConsole());

        CodexUsageStatus? status = await gate.WaitForCapacityAsync(CancellationToken.None);

        Assert.IsType<NullUsageGate>(gate);
        Assert.Null(status);
    }

    [Fact]
    public void IsEnabled_DefaultsToFalseWithoutTheEnvironmentVariable()
    {
        string? previous = Environment.GetEnvironmentVariable("COMMANDCENTER_USAGE_GATE");
        try
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", null);
            Assert.False(UsageGateComposition.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", previous);
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    public void IsEnabled_TrueWhenTheEnvironmentVariableOptsIn(string flag)
    {
        string? previous = Environment.GetEnvironmentVariable("COMMANDCENTER_USAGE_GATE");
        try
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", flag);
            Assert.True(UsageGateComposition.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", previous);
        }
    }

    [Fact]
    public void Create_WhenEnabled_ReturnsTheRealWatermarkGate()
    {
        string? previous = Environment.GetEnvironmentVariable("COMMANDCENTER_USAGE_GATE");
        try
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", "1");
            IUsageGate gate = UsageGateComposition.Create(new FakeCodexUsageProbe(), new FakeUsageDelay(), new RecordingLoopConsole());

            Assert.IsType<UsageGate>(gate);
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_USAGE_GATE", previous);
        }
    }
}
