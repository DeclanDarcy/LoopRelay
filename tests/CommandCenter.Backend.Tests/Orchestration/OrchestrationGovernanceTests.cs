using System.Reflection;
using CommandCenter.Backend.Endpoints;
using CommandCenter.Execution.Services;
using CommandCenter.Orchestration.Services;
using Xunit;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// m11 governance guard for the orchestration loop's ROLLBACK SURFACE — the one boundary documented in
/// <c>docs/orchestration-loop-governance.md</c> that no earlier milestone test protected. The layering,
/// prompt-authority, endpoint-disposition, decision-isolation, process-leak, recovery, and per-flag OFF-branch
/// boundaries are already guarded (see the governance test coverage map); this pins what remains:
/// (1) the four feature-flag DEFAULTS that make a default-constructed overlay a no-op (rollback paths 2-4),
/// (2) the documented router transfer threshold default, and
/// (3) the continued existence of the legacy execution-session subsystem (rollback path 5).
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

        // Defaults reproduce today's production behavior byte-for-byte; each flag is an opt-OUT (or, for the
        // transfer fallback, opt-IN) a deployment flips. Documented as rollback paths 2-4.
        Assert.True(flags.PersistentPlanningProcessEnabled);          // rollback path 2 off-switch
        Assert.True(flags.PersistentDecisionProcessReuseEnabled);     // rollback path 3 off-switch
        Assert.False(flags.TransferOnlyDecisionFallbackEnabled);      // rollback path 3 forced-transfer opt-in
        Assert.True(flags.AutomaticCommitPushAfterExecuteEnabled);    // rollback path 4 off-switch
    }

    [Fact]
    public void Router_transfer_threshold_default_is_pinned()
    {
        // The documented registry-free router default. Raising it makes the router always Continue (a rollback
        // lever); pinning it keeps docs/orchestration-loop-governance.md (LOOP-6) faithful.
        Assert.Equal(200_000, new DecisionSessionRouterOptions().DecisionTokenTransferThreshold);
    }

    [Fact]
    public void Legacy_execution_session_subsystem_remains_available_as_rollback_path_five()
    {
        // Rollback path 5: return to the existing execution/session endpoints. The legacy subsystem must remain
        // wired and unmodified. typeof(...) makes deletion a compile break; the reflected map-method lookups make
        // a rename a test failure with a clear message rather than a silent loss of the rollback path.
        Assert.NotNull(
            typeof(ExecutionSessionsEndpoints).GetMethod(
                nameof(ExecutionSessionsEndpoints.MapExecutionSessionsEndpoints),
                BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(
            typeof(ExecutionEndpoints).GetMethod(
                nameof(ExecutionEndpoints.MapExecutionEndpoints),
                BindingFlags.Public | BindingFlags.Static));

        // The legacy services that own the AwaitingAcceptance flow the orchestration loop intentionally diverges
        // from (docs/orchestration-loop-governance.md, DIVERGENCE-AWAITING-ACCEPTANCE) must still exist so the
        // rollback target is real.
        Assert.NotNull(typeof(HandoffService));
        Assert.NotNull(typeof(ExecutionSessionService));
    }
}
