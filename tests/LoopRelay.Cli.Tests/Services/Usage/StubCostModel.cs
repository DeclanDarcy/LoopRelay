using LoopRelay.Agents.Models;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Primitives;

namespace LoopRelay.Cli.Tests.Services;


/// <summary>Cost model with directly-controlled scalars so a CLI test can drive the economic marginal rule.</summary>
internal sealed class StubCostModel : IDecisionCostModel
{
    public double MeasureValue { get; set; }
    public double EstimateValue { get; set; }
    public double Measure(AgentTokenUsage turn) => MeasureValue;
    public double EstimateNextCycle(DecisionCostForecast forecast) => EstimateValue;
}
