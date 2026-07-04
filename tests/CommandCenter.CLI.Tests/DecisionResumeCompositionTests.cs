using Xunit;

namespace CommandCenter.Cli.Tests;

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
        string? original = Environment.GetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME");
        try
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME", value);
            Assert.Equal(expected, DecisionResumeComposition.IsEnabled());
        }
        finally
        {
            Environment.SetEnvironmentVariable("COMMANDCENTER_DECISION_RESUME", original);
        }
    }
}
