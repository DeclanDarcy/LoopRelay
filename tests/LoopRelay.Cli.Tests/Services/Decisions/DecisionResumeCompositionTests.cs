using LoopRelay.Cli.Services.Decisions;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Decisions;

public sealed class DecisionResumeCompositionTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    public void IsEnabled_HonorsTheKillSwitch(string? value, bool expected)
    {
        string? original = Environment.GetEnvironmentVariable("LoopRelay_DECISION_RESUME");
        try
        {
            Environment.SetEnvironmentVariable("LoopRelay_DECISION_RESUME", value);
            Assert.Equal(expected, DecisionResumeComposition.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LoopRelay_DECISION_RESUME", original);
        }
    }
}
