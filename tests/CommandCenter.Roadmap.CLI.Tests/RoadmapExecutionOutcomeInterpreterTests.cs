using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapExecutionOutcomeInterpreterTests
{
    [Theory]
    [InlineData("Epic Complete", "EpicComplete")]
    [InlineData("Continue Required", "ContinueRequired")]
    [InlineData("Execution Blocked", "ExecutionBlocked")]
    public void Completed_transport_is_interpreted_from_explicit_execution_disposition(
        string status,
        string expectedKind)
    {
        RoadmapExecutionOutcome outcome = new RoadmapExecutionOutcomeInterpreter().Interpret(
            RoadmapExecutionTransportResult.Completed(Disposition(status)));

        Assert.Equal(Enum.Parse<RoadmapExecutionOutcomeKind>(expectedKind), outcome.Kind);
        Assert.NotNull(outcome.Disposition);
        Assert.Equal(status, outcome.Disposition!.StatusText);
        Assert.Equal("High", outcome.Disposition.Confidence);
        Assert.Equal("Evidence for the execution outcome.", outcome.Message);
    }

    [Fact]
    public void Completed_transport_without_disposition_is_malformed_output()
    {
        RoadmapExecutionOutcome outcome = new RoadmapExecutionOutcomeInterpreter().Interpret(
            RoadmapExecutionTransportResult.Completed("# Execution Report\n\nWork completed one milestone."));

        Assert.Equal(RoadmapExecutionOutcomeKind.MalformedOutput, outcome.Kind);
        Assert.Contains("Execution Disposition", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_completed_transport_is_runtime_failure()
    {
        RoadmapExecutionOutcome outcome = new RoadmapExecutionOutcomeInterpreter().Interpret(
            RoadmapExecutionTransportResult.Failed("Failed", "agent process failed"));

        Assert.Equal(RoadmapExecutionOutcomeKind.RuntimeFailure, outcome.Kind);
        Assert.Equal("agent process failed", outcome.Message);
        Assert.Null(outcome.Disposition);
    }

    private static string Disposition(string status) => $$"""
        # Execution Report

        ## Execution Disposition

        | Field | Value |
        |---|---|
        | Status | {{status}} |
        | Confidence | High |
        | Evidence Summary | Evidence for the execution outcome. |
        | Next Step | ContinueExecution |
        """;
}
