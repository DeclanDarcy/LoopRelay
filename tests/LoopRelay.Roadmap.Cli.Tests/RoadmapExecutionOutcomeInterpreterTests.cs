using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapExecutionOutcomeInterpreterTests
{
    [Theory]
    [InlineData("Epic Complete", "EvaluateEpicCompletionAndDrift", "EpicComplete")]
    [InlineData("Continue Required", "ContinueExecution", "ContinueRequired")]
    [InlineData("Execution Blocked", "ResolveExecutionBlocker", "ExecutionBlocked")]
    public void Completed_transport_is_interpreted_from_valid_execution_protocol(
        string status,
        string nextStep,
        string expectedKind)
    {
        Cli.RoadmapExecutionOutcome outcome = new Cli.RoadmapExecutionOutcomeInterpreter().Interpret(
            Cli.RoadmapExecutionTransportResult.Completed(Disposition(status, nextStep)));

        Assert.Equal(Enum.Parse<Cli.RoadmapExecutionOutcomeKind>(expectedKind), outcome.Kind);
        Assert.NotNull(outcome.Disposition);
        Assert.Equal(status, outcome.Disposition!.StatusText);
        Assert.Equal(nextStep, outcome.Disposition.NextStepText);
        Assert.Equal("High", outcome.Disposition.Confidence);
        Assert.Equal("Evidence for the execution outcome.", outcome.Message);
        Assert.NotNull(outcome.ProtocolValidation);
        Assert.True(outcome.ProtocolValidation!.IsValid);
        Assert.Equal(nextStep, outcome.ProtocolValidation.RequiredRecoveryPath);
    }

    [Fact]
    public void Completed_transport_without_disposition_is_malformed_output()
    {
        Cli.RoadmapExecutionOutcome outcome = new Cli.RoadmapExecutionOutcomeInterpreter().Interpret(
            Cli.RoadmapExecutionTransportResult.Completed("# Execution Report\n\nWork completed one milestone."));

        Assert.Equal(Cli.RoadmapExecutionOutcomeKind.MalformedOutput, outcome.Kind);
        Assert.Contains("Execution Disposition", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_transport_with_contradictory_protocol_is_malformed_output()
    {
        Cli.RoadmapExecutionOutcome outcome = new Cli.RoadmapExecutionOutcomeInterpreter().Interpret(
            Cli.RoadmapExecutionTransportResult.Completed(Disposition("Epic Complete", "ContinueExecution")));

        Assert.Equal(Cli.RoadmapExecutionOutcomeKind.MalformedOutput, outcome.Kind);
        Assert.NotNull(outcome.Disposition);
        Assert.Equal("Epic Complete", outcome.Disposition!.StatusText);
        Assert.Equal("ContinueExecution", outcome.Disposition.NextStepText);
        Assert.NotNull(outcome.ProtocolValidation);
        Assert.False(outcome.ProtocolValidation!.IsValid);
        Assert.Contains("protocol violation", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Completed_transport_with_unknown_next_step_command_is_malformed_output()
    {
        Cli.RoadmapExecutionOutcome outcome = new Cli.RoadmapExecutionOutcomeInterpreter().Interpret(
            Cli.RoadmapExecutionTransportResult.Completed(Disposition("Continue Required", "UnknownExecutionCommand")));

        Assert.Equal(Cli.RoadmapExecutionOutcomeKind.MalformedOutput, outcome.Kind);
        Assert.Null(outcome.Disposition);
        Assert.Contains("unsupported value `UnknownExecutionCommand`", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Non_completed_transport_is_runtime_failure()
    {
        Cli.RoadmapExecutionOutcome outcome = new Cli.RoadmapExecutionOutcomeInterpreter().Interpret(
            Cli.RoadmapExecutionTransportResult.Failed("Failed", "agent process failed"));

        Assert.Equal(Cli.RoadmapExecutionOutcomeKind.RuntimeFailure, outcome.Kind);
        Assert.Equal("agent process failed", outcome.Message);
        Assert.Null(outcome.Disposition);
    }

    [Fact]
    public void Parser_decodes_valid_execution_disposition()
    {
        Cli.ExecutionDisposition disposition = new Cli.ExecutionDispositionParser().Parse(
            Disposition("Continue Required", "ContinueExecution"));

        Assert.Equal(Cli.ExecutionDispositionStatus.ContinueRequired, disposition.Status);
        Assert.Equal(Cli.ExecutionDispositionCommand.ContinueExecution, disposition.NextStep);
        Assert.Equal("Evidence for the execution outcome.", disposition.EvidenceSummary);
    }

    [Fact]
    public void Parser_rejects_malformed_structure()
    {
        Cli.MarkdownParseException exception = Assert.Throws<Cli.MarkdownParseException>(
            () => new Cli.ExecutionDispositionParser().Parse("# Execution Report\n\nNo disposition."));

        Assert.Contains("Execution Disposition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_rejects_unknown_status()
    {
        Cli.MarkdownParseException exception = Assert.Throws<Cli.MarkdownParseException>(
            () => new Cli.ExecutionDispositionParser().Parse(Disposition("Finished", "ContinueExecution")));

        Assert.Contains("status has unsupported value `Finished`", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_rejects_unknown_next_step_command()
    {
        Cli.MarkdownParseException exception = Assert.Throws<Cli.MarkdownParseException>(
            () => new Cli.ExecutionDispositionParser().Parse(Disposition("Continue Required", "KeepGoing")));

        Assert.Contains("command has unsupported value `KeepGoing`", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_does_not_apply_protocol_policy_to_known_pairs()
    {
        Cli.ExecutionDisposition disposition = new Cli.ExecutionDispositionParser().Parse(
            Disposition("Epic Complete", "ContinueExecution"));

        Assert.Equal(Cli.ExecutionDispositionStatus.EpicComplete, disposition.Status);
        Assert.Equal(Cli.ExecutionDispositionCommand.ContinueExecution, disposition.NextStep);
    }

    private static string Disposition(string status, string nextStep) => $$"""
        # Execution Report

        ## Execution Disposition

        | Field | Value |
        |---|---|
        | Status | {{status}} |
        | Confidence | High |
        | Evidence Summary | Evidence for the execution outcome. |
        | Next Step | {{nextStep}} |
        """;
}
