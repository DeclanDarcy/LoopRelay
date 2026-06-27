namespace CommandCenter.Orchestration.Models;

/// <summary>
/// Inputs the Phase 7 router consumes to choose Continue (reuse the warm Decision process) vs
/// Transfer (seed a fresh Decision process). Held as transient run state by the orchestrator;
/// populated as turns complete. Token pressure on the active sessions is the routing signal.
/// </summary>
public sealed record RouterInputs(int DecisionSessionTokens, int OperationalSessionTokens)
{
    public static RouterInputs Empty { get; } = new(0, 0);
}
