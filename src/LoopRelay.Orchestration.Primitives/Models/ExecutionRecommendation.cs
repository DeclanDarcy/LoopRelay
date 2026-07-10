using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Models;

public sealed record ExecutionRecommendation(AgentModel Model, AgentEffort Effort);

public sealed record PersistedExecutionRecommendation(
    AgentModel Model,
    AgentEffort Effort,
    string PromptHash);

public sealed record ValidatedExecutionRecommendation
{
    internal ValidatedExecutionRecommendation(
        string prompt,
        AgentModel model,
        AgentEffort effort,
        string promptHash)
    {
        Prompt = prompt;
        Model = model;
        Effort = effort;
        PromptHash = promptHash;
    }

    public string Prompt { get; }

    public AgentModel Model { get; }

    public AgentEffort Effort { get; }

    public string PromptHash { get; }
}
