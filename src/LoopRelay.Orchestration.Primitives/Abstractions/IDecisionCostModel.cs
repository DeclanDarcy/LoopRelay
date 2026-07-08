using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Primitives;

namespace LoopRelay.Orchestration.Abstractions;

/// <summary>
/// Converts turn telemetry into the single, abstract scalar the decision router optimizes — "cost". The router
/// is unit-blind: it never knows whether cost is effective tokens, dollars, latency, or a weighted blend. To
/// keep the average-cost optimization valid, an implementation MUST (1) return one consistent, additive scalar
/// in a single currency for BOTH reuse turns and transfer turns (so <c>R</c>, <c>C</c>, and the prediction are
/// comparable), and (2) be monotone non-decreasing in run age — otherwise the marginal rule degrades from
/// "globally optimal" to a local heuristic backed only by the capacity guard.
/// </summary>
public interface IDecisionCostModel
{
    /// <summary>The cost of one completed turn, from its token telemetry.</summary>
    double Measure(AgentTokenUsage turn);

    /// <summary>Predicts the next reuse cycle's cost (used for the marginal stopping rule).</summary>
    double EstimateNextCycle(DecisionCostForecast forecast);
}
