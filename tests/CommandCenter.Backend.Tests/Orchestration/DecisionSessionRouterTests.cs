using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>
/// Router Reuse and Transfer (m7): the registry-free <see cref="DecisionSessionRouter"/> decides reuse-vs-transfer
/// from unit-blind cost signals. The hard CAPACITY guard (occupancy ≥ a fraction of the window) always applies on
/// top of the economic <see cref="DecisionTransferPolicy"/>. The default policy is the online average-cost optimum
/// (<see cref="DecisionTransferPolicy.MarginalAverageCost"/>): transfer once the predicted next cycle would raise
/// the run's amortized average, <c>eNext ≥ (R + C) / n</c>. <see cref="DecisionTransferPolicy.LinearReuseApprox"/>
/// (R ≥ C) and <see cref="DecisionTransferPolicy.CapacityOnly"/> are distinct, explicitly-named policies.
/// </summary>
public sealed class DecisionSessionRouterTests
{
    private static RouterInputs Inputs(
        int occupancy = 0,
        double reuseCost = 0d,
        int cycles = 0,
        double predictedNext = 0d,
        double transferCost = 0d) =>
        new(occupancy, reuseCost, cycles, predictedNext, transferCost);

    // ---- defaults / governed surface ----

    [Fact]
    public void The_default_policy_is_marginal_average_cost_with_a_high_capacity_guard()
    {
        var options = new DecisionSessionRouterOptions();
        Assert.Equal(DecisionTransferPolicy.MarginalAverageCost, options.Policy);
        Assert.Equal(256_000, options.ModelContextWindowTokens);
        Assert.Equal(0.90, options.CapacityGuardFraction);
        Assert.Equal(230_400, options.CapacityGuardTokens); // 256_000 * 0.90
    }

    // ---- hard capacity guard (applies under every policy) ----

    [Fact]
    public void Occupancy_at_or_above_the_capacity_guard_always_transfers_even_under_capacity_only()
    {
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(
            ModelContextWindowTokens: 1_000, CapacityGuardFraction: 0.90, Policy: DecisionTransferPolicy.CapacityOnly));
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(Inputs(occupancy: 900)));
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(Inputs(occupancy: 5_000)));
    }

    [Fact]
    public void Capacity_only_never_transfers_below_the_guard_however_expensive_reuse_is()
    {
        var router = new DecisionSessionRouter(new DecisionSessionRouterOptions(
            ModelContextWindowTokens: 1_000, CapacityGuardFraction: 0.90, Policy: DecisionTransferPolicy.CapacityOnly));
        // Below the 900 guard, with economics that WOULD transfer under other policies — CapacityOnly still continues.
        Assert.Equal(DecisionRoute.Continue, router.Evaluate(Inputs(occupancy: 899, reuseCost: 1_000_000, cycles: 1, predictedNext: 1_000_000, transferCost: 1)));
    }

    // ---- marginal average-cost policy: eNext ≥ (R + C) / n ----

    [Fact]
    public void Marginal_continues_while_the_next_cycle_is_cheaper_than_the_amortized_average()
    {
        var router = Marginal();
        // (R + C)/n = (100 + 50)/3 = 50; next cycle (49) is cheaper → keep reusing.
        Assert.Equal(DecisionRoute.Continue, router.Evaluate(Inputs(occupancy: 100, reuseCost: 100, cycles: 3, predictedNext: 49, transferCost: 50)));
    }

    [Fact]
    public void Marginal_transfers_once_the_next_cycle_would_raise_the_amortized_average()
    {
        var router = Marginal();
        // next cycle (50) ≥ average (50) → reset.
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(Inputs(occupancy: 100, reuseCost: 100, cycles: 3, predictedNext: 50, transferCost: 50)));
    }

    [Fact]
    public void Marginal_never_transfers_before_the_first_cycle_completes()
    {
        var router = Marginal();
        // n == 0 (just-reseeded process): always continue, regardless of a large predicted next cost (no div-by-zero).
        Assert.Equal(DecisionRoute.Continue, router.Evaluate(Inputs(occupancy: 100, reuseCost: 0, cycles: 0, predictedNext: 999_999, transferCost: 50)));
    }

    // ---- linear-reuse-approximation policy (diagnostic): R ≥ C ----

    [Fact]
    public void Linear_reuse_approx_transfers_when_accumulated_reuse_reaches_the_transfer_cost()
    {
        var router = new DecisionSessionRouter(WideWindow(DecisionTransferPolicy.LinearReuseApprox));
        Assert.Equal(DecisionRoute.Continue, router.Evaluate(Inputs(occupancy: 100, reuseCost: 49, cycles: 5, predictedNext: 999, transferCost: 50)));
        Assert.Equal(DecisionRoute.Transfer, router.Evaluate(Inputs(occupancy: 100, reuseCost: 50, cycles: 5, predictedNext: 0, transferCost: 50)));
    }

    private static DecisionSessionRouter Marginal() => new(WideWindow(DecisionTransferPolicy.MarginalAverageCost));

    // A window large enough that the capacity guard never fires for these small occupancies, isolating the economic policy.
    private static DecisionSessionRouterOptions WideWindow(DecisionTransferPolicy policy) =>
        new(ModelContextWindowTokens: 1_000_000, CapacityGuardFraction: 0.90, Policy: policy);
}
