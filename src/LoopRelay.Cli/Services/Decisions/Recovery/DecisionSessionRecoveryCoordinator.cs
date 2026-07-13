using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Cli.Services.Decisions.Recovery;

internal sealed record DecisionSessionRecoveryResult(
    IAgentSession Session,
    bool Seeded,
    DecisionSessionActiveState Active,
    DecisionSessionLineageNode Lineage,
    RecoveryRuntimeResult Recovery);

/// <summary>Maps decision lifecycle data to the provider-neutral runtime; policy and mechanisms live elsewhere.</summary>
internal sealed class DecisionSessionRecoveryCoordinator(
    IRecoveryRuntime _runtime,
    IRecoveryStore _store)
{
    public async Task<DecisionSessionRecoveryResult> OpenAsync(
        string scopeId,
        string? transitionRunId,
        AgentSessionSpec resumeSpec,
        AgentSessionSpec freshSpec,
        SessionContinuityProfile profile,
        IReadOnlyDictionary<string, string> policy,
        int contextBudget,
        LoopRelay.Core.Models.Identity.CanonicalCausalContext? causality,
        CancellationToken cancellationToken)
    {
        RecoveryRuntimeResult result = await _runtime.RunAsync(
            new RecoveryRuntimeRequest(
                scopeId,
                transitionRunId,
                resumeSpec,
                freshSpec,
                profile,
                policy,
                contextBudget,
                "ExecuteRestart",
                causality),
            cancellationToken);
        if (result.Session is null)
        {
            throw new InvalidOperationException(
                $"Decision continuity stopped with {result.Outcome}: {result.Diagnostic ?? "no diagnostic"}");
        }

        ActiveStateReadResult active = await _store.ReadActiveAsync(scopeId, cancellationToken);
        if (active.Status != ActiveStateReadStatus.Present || active.Active is null || active.Lineage is null)
        {
            await result.Session.DisposeAsync();
            throw new InvalidOperationException(
                $"Decision continuity returned a session but active state is {active.Status}: {active.Diagnostic}");
        }

        return new DecisionSessionRecoveryResult(
            result.Session,
            Seeded: true,
            active.Active,
            active.Lineage,
            result);
    }
}
