using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Tunable knobs for <see cref="DecisionSessionRouter"/> (m7). <see cref="DecisionTokenTransferThreshold"/>
/// is the decision-session token pressure at or above which the router elects to Transfer (recycle) rather
/// than reuse the warm process. The default is a conservative fraction of a large model context window, so
/// reuse is the steady state and Transfer fires only once a single Decision process has accumulated enough
/// proposal history that recycling it (preserving continuity through operational context) is worthwhile.
/// </summary>
public sealed record DecisionSessionRouterOptions(int DecisionTokenTransferThreshold = 200_000);

/// <summary>
/// Default <see cref="IDecisionSessionRouter"/>: a pure, deterministic threshold over the loop's observed (or
/// deterministically estimated) decision-session token pressure (m7). No registry, no I/O, no async — the
/// orchestrator owns the inputs and the eligibility gate. Routing this way keeps the Plan-Authoring loop
/// registry-free (it never creates the DecisionSessions registry aggregates the lifecycle policy requires)
/// while still honouring the spec's "route on the active decision session token count or a deterministic
/// fallback estimate" intent.
/// </summary>
public sealed class DecisionSessionRouter : IDecisionSessionRouter
{
    private readonly DecisionSessionRouterOptions options;

    public DecisionSessionRouter(DecisionSessionRouterOptions? options = null)
    {
        this.options = options ?? new DecisionSessionRouterOptions();
    }

    public DecisionRoute Evaluate(RouterInputs inputs) =>
        inputs.DecisionSessionTokens >= options.DecisionTokenTransferThreshold
            ? DecisionRoute.Transfer
            : DecisionRoute.Continue;
}
