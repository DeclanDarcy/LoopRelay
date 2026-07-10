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

    [Theory]
    [InlineData(null, "ResumeOnly")]
    [InlineData("resume-only", "ResumeOnly")]
    [InlineData("reconstructed", "Reconstructed")]
    [InlineData("certified", "Certified")]
    public void RecoveryPolicyIsExplicitAndDefaultsToResumeOnly(
        string? value,
        string expected)
    {
        string? original = Environment.GetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable);
        try
        {
            Environment.SetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable, value);
            Assert.Equal(expected, DecisionResumeComposition.RecoveryPolicy().ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable, original);
        }
    }

    [Fact]
    public void InvalidRecoveryPolicyFailsClosed()
    {
        string? original = Environment.GetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable);
        try
        {
            Environment.SetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable, "guess");
            Assert.Throws<InvalidOperationException>(() => DecisionResumeComposition.RecoveryPolicy());
        }
        finally
        {
            Environment.SetEnvironmentVariable(DecisionResumeComposition.RecoveryPolicyVariable, original);
        }
    }
}
