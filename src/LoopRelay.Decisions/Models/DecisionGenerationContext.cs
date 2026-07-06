namespace LoopRelay.Decisions.Models;

public sealed record DecisionGenerationContext(
    Guid RepositoryId,
    string Fingerprint,
    IReadOnlyList<DecisionGenerationContextEntry> Goals,
    IReadOnlyList<DecisionGenerationContextEntry> Constraints,
    IReadOnlyList<DecisionGenerationContextEntry> Risks,
    IReadOnlyList<DecisionGenerationContextEntry> Questions,
    IReadOnlyList<DecisionGenerationContextEntry> PriorDecisions,
    IReadOnlyList<DecisionGenerationContextEntry> RepositoryState,
    IReadOnlyList<DecisionGenerationContextEntry> Dependencies,
    IReadOnlyList<DecisionGenerationContextEntry> HandoffState,
    IReadOnlyList<string> Diagnostics)
{
    public static DecisionGenerationContext Empty(Guid repositoryId)
    {
        return new DecisionGenerationContext(
            repositoryId,
            string.Empty,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }
}
