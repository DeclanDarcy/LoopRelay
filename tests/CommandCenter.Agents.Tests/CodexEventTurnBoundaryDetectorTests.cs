using CommandCenter.Agents.Models;
using CommandCenter.Agents.Services;

namespace CommandCenter.Agents.Tests;

public sealed class CodexEventTurnBoundaryDetectorTests
{
    private readonly CodexEventTurnBoundaryDetector detector = new();

    [Fact]
    public void ExecTurnCompletedEndsTheTurnWithReportedUsage()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"type":"turn.completed","usage":{"input_tokens":120,"output_tokens":45}}""");

        Assert.Equal(AgentLineClassification.TurnCompleted, inspection.Classification);
        Assert.NotNull(inspection.Usage);
        Assert.Equal(120, inspection.Usage!.PromptTokens);
        Assert.Equal(45, inspection.Usage.OutputTokens);
    }

    [Fact]
    public void ExecTurnCompletedCapturesCachedInputTokens()
    {
        // The cached subset of the input is surfaced so the cost model can compute cache-adjusted reuse cost.
        AgentLineInspection inspection = detector.Inspect(
            """{"type":"turn.completed","usage":{"input_tokens":120,"cached_input_tokens":90,"output_tokens":45}}""");

        Assert.Equal(120, inspection.Usage!.PromptTokens);
        Assert.Equal(90, inspection.Usage.CachedInputTokens);
        Assert.Equal(45, inspection.Usage.OutputTokens);
    }

    [Fact]
    public void AppServerTurnCompletedMethodWithUsageUnderParamsEndsTheTurn()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"method":"turn/completed","params":{"usage":{"input_tokens":10,"output_tokens":20}}}""");

        Assert.Equal(AgentLineClassification.TurnCompleted, inspection.Classification);
        Assert.Equal(10, inspection.Usage!.PromptTokens);
        Assert.Equal(20, inspection.Usage.OutputTokens);
    }

    [Fact]
    public void TurnCompletedWithoutUsageHasNullUsageSoTheEstimatorCanFallBack()
    {
        AgentLineInspection inspection = detector.Inspect("""{"type":"turn.completed"}""");

        Assert.Equal(AgentLineClassification.TurnCompleted, inspection.Classification);
        Assert.Null(inspection.Usage);
    }

    [Fact]
    public void ExecAgentMessageItemSurfacesItsTextAsOutput()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"type":"item.completed","item":{"type":"agent_message","text":"Hello world"}}""");

        Assert.Equal(AgentLineClassification.Output, inspection.Classification);
        Assert.Equal("Hello world", inspection.Content);
    }

    [Fact]
    public void AppServerAssistantMessageUnderParamsSurfacesItsText()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"method":"item/completed","params":{"item":{"type":"assistant_message","text":"Hi"}}}""");

        Assert.Equal(AgentLineClassification.Output, inspection.Classification);
        Assert.Equal("Hi", inspection.Content);
    }

    [Fact]
    public void AgentMessageContentArrayIsConcatenated()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"type":"item.completed","item":{"type":"agent_message","content":[{"type":"output_text","text":"a"},{"type":"output_text","text":"b"}]}}""");

        Assert.Equal(AgentLineClassification.Output, inspection.Classification);
        Assert.Equal("ab", inspection.Content);
    }

    [Fact]
    public void AgentMessageDeltaSurfacesTheDeltaText()
    {
        AgentLineInspection inspection = detector.Inspect(
            """{"method":"item/agentMessage/delta","params":{"delta":"chunk"}}""");

        Assert.Equal(AgentLineClassification.Output, inspection.Classification);
        Assert.Equal("chunk", inspection.Content);
    }

    [Fact]
    public void ReasoningAndLifecycleEventsAreIgnoredSoTheyNeverPolluteOutput()
    {
        Assert.Equal(
            AgentLineClassification.Ignored,
            detector.Inspect("""{"type":"item.completed","item":{"type":"reasoning","text":"thinking"}}""").Classification);
        Assert.Equal(
            AgentLineClassification.Ignored,
            detector.Inspect("""{"type":"thread.started","thread_id":"abc"}""").Classification);
        Assert.Equal(
            AgentLineClassification.Ignored,
            detector.Inspect("""{"type":"item.started","item":{"type":"command_execution"}}""").Classification);
    }

    [Fact]
    public void NonJsonLineIsSurfacedVerbatimRatherThanLost()
    {
        AgentLineInspection inspection = detector.Inspect("not a json event");

        Assert.Equal(AgentLineClassification.Output, inspection.Classification);
        Assert.Equal("not a json event", inspection.Content);
    }

    [Fact]
    public void BlankLineIsIgnored()
    {
        Assert.Equal(AgentLineClassification.Ignored, detector.Inspect("   ").Classification);
    }
}
