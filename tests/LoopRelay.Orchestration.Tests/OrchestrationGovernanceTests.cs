using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;
using Xunit;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// m11 governance guard for the orchestration loop's ROLLBACK SURFACE — the one boundary documented in
/// <c>docs/orchestration-loop-governance.md</c> that no earlier milestone test protected. The layering,
/// prompt-authority, endpoint-disposition, decision-isolation, process-leak, recovery, and per-flag OFF-branch
/// boundaries are already guarded (see the governance test coverage map); this pins what remains:
/// (1) the five feature-flag DEFAULTS — four make a default-constructed overlay a no-op (rollback paths 2-4);
///     the fifth (SandboxOperationalContextEvolution) defaults ON to the Stage-2 sandboxed evolution, with OFF
///     as its rollback lever, and
/// (2) the documented router transfer threshold default.
/// A change that silently flips a default or deletes a rollback path now fails here instead of silently
/// invalidating the documented rollback contract.
///
/// Pure unit assertions (no host boot, no process-global mutation), so this class is intentionally NOT in the
/// ProcessEnvironment serialized collection.
/// </summary>
public sealed class OrchestrationGovernanceTests
{
    [Fact]
    public void Feature_flag_defaults_preserve_todays_behavior_as_the_rollback_off_switch_surface()
    {
        var flags = new OrchestrationFeatureFlags();

        // The m10 flags reproduce today's production behavior byte-for-byte; each is an opt-OUT (or, for the
        // transfer fallback, opt-IN) a deployment flips (rollback paths 2-4). The Stage-2 sandbox flag is the one
        // exception: it defaults ON to the new sandboxed evolution, and OFF is its rollback lever.
        Assert.True(flags.PersistentPlanningProcessEnabled);          // rollback path 2 off-switch
        Assert.True(flags.PersistentDecisionProcessReuseEnabled);     // rollback path 3 off-switch
        Assert.False(flags.TransferOnlyDecisionFallbackEnabled);      // rollback path 3 forced-transfer opt-in
        Assert.True(flags.AutomaticCommitPushAfterExecuteEnabled);    // rollback path 4 off-switch
        Assert.True(flags.SandboxOperationalContextEvolutionEnabled); // Stage 2: sandbox on by default; OFF reverts to repo-cwd evolution
    }

    [Fact]
    public void Router_transfer_policy_default_is_pinned()
    {
        // The documented registry-free router default: the online average-cost optimum (MarginalAverageCost) with a
        // hard capacity guard at 90% of a 256k window (230,400 tokens of OCCUPANCY). Changing the policy, window, or
        // guard fraction is a rollback lever; pinning them keeps docs/orchestration-loop-governance.md (LOOP-6) faithful.
        var options = new DecisionSessionRouterOptions();
        Assert.Equal(DecisionTransferPolicy.MarginalAverageCost, options.Policy);
        Assert.Equal(256_000, options.ModelContextWindowTokens);
        Assert.Equal(0.90, options.CapacityGuardFraction);
        Assert.Equal(230_400, options.CapacityGuardTokens);
    }
}
