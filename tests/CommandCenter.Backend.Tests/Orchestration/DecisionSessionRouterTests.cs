using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// Router Reuse and Transfer (m7): the default <see cref="DecisionSessionRouter"/> is a pure, deterministic
/// threshold over decision-session token pressure. Below the threshold it reuses the warm process (Continue);
/// at or above it, it elects to recycle (Transfer). It is registry-free — it routes on the loop's own
/// <see cref="RouterInputs"/> signal, never on the registry-backed lifecycle policy — so Transfer is reachable
/// without any DecisionSessions registry state. These lock the token-threshold routing the milestone certifies.
/// </summary>
public sealed class DecisionSessionRouterTests
{
    [Fact]
    public void Pressure_below_the_threshold_routes_continue()
    {
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(DecisionTokenTransferThreshold: 1_000));

        Assert.Equal(DecisionRoute.Continue, router.Evaluate(new RouterInputs(DecisionSessionTokens: 999, OperationalSessionTokens: 0)));
    }

    [Fact]
    public void Pressure_at_or_above_the_threshold_routes_transfer()
    {
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(DecisionTokenTransferThreshold: 1_000));

        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(new RouterInputs(DecisionSessionTokens: 1_000, OperationalSessionTokens: 0)));
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(new RouterInputs(DecisionSessionTokens: 5_000, OperationalSessionTokens: 0)));
    }

    [Fact]
    public void Operational_pressure_alone_never_recycles_the_decision_process()
    {
        // Only DECISION-session pressure recycles the decision process; cumulative operational pressure is incidental.
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(DecisionTokenTransferThreshold: 1_000));

        Assert.Equal(DecisionRoute.Continue, router.Evaluate(new RouterInputs(DecisionSessionTokens: 0, OperationalSessionTokens: 1_000_000)));
    }

    [Fact]
    public void The_conservative_default_threshold_keeps_reuse_as_the_steady_state()
    {
        var router = new DecisionSessionRouter();

        // A modest decision session stays on Continue under the default; only a very large one transfers.
        Assert.Equal(DecisionRoute.Continue, router.Evaluate(new RouterInputs(DecisionSessionTokens: 50_000, OperationalSessionTokens: 0)));
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(new RouterInputs(DecisionSessionTokens: 250_000, OperationalSessionTokens: 0)));
    }
}
