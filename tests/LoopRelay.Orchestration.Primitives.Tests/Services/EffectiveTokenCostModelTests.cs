using LoopRelay.Agents.Models;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Primitives.Tests;

/// <summary>
/// The default cost model (cache-adjusted effective tokens) is the one concrete <c>IDecisionCostModel</c> shipped
/// and the whole basis of "cost-aware" routing, so its cache discount, defensive clamps, and next-cycle prediction
/// are pinned directly here (the router/orchestrator tests feed it only cached=0 usages, which can't exercise these).
/// </summary>
public sealed class EffectiveTokenCostModelTests
{
    private static readonly EffectiveTokenCostModel Model = new(); // default k = 0.10

    [Fact]
    public void Measure_discounts_cached_input_below_fresh_input()
    {
        // prompt 100 (40 cached, 60 fresh) + 20 output => 60 + 40*0.10 + 20 = 84. Cache-blind would be 120.
        Assert.Equal(84d, Model.Measure(new AgentTokenUsage(PromptTokens: 100, OutputTokens: 20, CachedInputTokens: 40)), 4);
    }

    [Fact]
    public void Measure_charges_a_fully_cached_prompt_at_only_the_cache_factor()
    {
        // prompt 100 all cached, no output => 0 fresh + 100*0.10 = 10.
        Assert.Equal(10d, Model.Measure(new AgentTokenUsage(PromptTokens: 100, OutputTokens: 0, CachedInputTokens: 100)), 4);
    }

    [Fact]
    public void Measure_is_cache_aware_not_cache_blind()
    {
        // A regression to `fresh + cached + output` (dropping the k discount) would give 100 here, not 19.
        Assert.Equal(19d, Model.Measure(new AgentTokenUsage(PromptTokens: 100, OutputTokens: 0, CachedInputTokens: 90)), 4);
    }

    [Fact]
    public void Measure_honours_a_custom_cache_cost_factor()
    {
        var model = new EffectiveTokenCostModel(cacheCostFactor: 0.5);
        // prompt 100 (80 cached, 20 fresh) => 20 + 80*0.5 = 60.
        Assert.Equal(60d, model.Measure(new AgentTokenUsage(PromptTokens: 100, OutputTokens: 0, CachedInputTokens: 80)), 4);
    }

    [Fact]
    public void Measure_clamps_cached_above_prompt_so_fresh_never_goes_negative()
    {
        // Malformed: cached (200) > prompt (50). Clamped to 50 -> fresh 0 -> 50*0.10 = 5 (not negative, no throw).
        Assert.Equal(5d, Model.Measure(new AgentTokenUsage(PromptTokens: 50, OutputTokens: 0, CachedInputTokens: 200)), 4);
    }

    [Fact]
    public void Measure_degrades_gracefully_on_negative_token_counts_instead_of_throwing()
    {
        // Regression guard: Math.Clamp(cached, 0, prompt) would THROW if prompt < 0. Everything floors at 0 first.
        double cost = Model.Measure(new AgentTokenUsage(PromptTokens: -5, OutputTokens: 10, CachedInputTokens: -3));
        Assert.Equal(10d, cost, 4); // prompt->0, cached->0, output 10
    }

    [Fact]
    public void EstimateNextCycle_extrapolates_a_rising_cost_by_its_velocity()
    {
        // last 100, prev 60 -> velocity +40 -> 140.
        Assert.Equal(140d, Model.EstimateNextCycle(new DecisionCostForecast(LastCycleCost: 100, PreviousCycleCost: 60)), 4);
    }

    [Fact]
    public void EstimateNextCycle_floors_a_falling_cost_at_the_last_observed()
    {
        // last 60, prev 100 -> velocity -40 -> max(60, 20) = 60 (never predict cheaper than the last observed).
        Assert.Equal(60d, Model.EstimateNextCycle(new DecisionCostForecast(LastCycleCost: 60, PreviousCycleCost: 100)), 4);
    }

    [Fact]
    public void EstimateNextCycle_returns_the_last_cost_when_no_history_exists()
    {
        Assert.Equal(0d, Model.EstimateNextCycle(new DecisionCostForecast(LastCycleCost: 0, PreviousCycleCost: 0)), 4);
    }
}
