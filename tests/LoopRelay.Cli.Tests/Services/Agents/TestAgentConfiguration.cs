using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Cli.Tests.Services.Agents;

internal static class TestAgentConfiguration
{
    public static BrainConfiguration Brain { get; } =
        new(AgentModel.Gpt56Sol, AgentEffort.XHigh);

    public static ExecutionRecommendation Execution { get; } =
        new(AgentModel.Gpt56Terra, AgentEffort.High);

    public static ValidatedExecutionRecommendation ValidatedExecution { get; } =
        ExecutionRecommendationContract.ValidatePair(
            "legacy-compiled-execution-path",
            ExecutionRecommendationContract.SerializePersisted(
                ExecutionRecommendationContract.Bind(
                    "legacy-compiled-execution-path",
                    Execution)));
}
