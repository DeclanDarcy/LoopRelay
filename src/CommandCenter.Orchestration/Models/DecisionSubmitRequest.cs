namespace CommandCenter.Orchestration.Models;

/// <summary>
/// Decision submit request (m5): the human-reviewed (possibly edited) decisions text the operator submits
/// through the review gate. The orchestrator persists it to <c>.agents/decisions/decisions.md</c> — the only
/// path by which captured decision output crosses into operational authority. Decisions text is required.
/// </summary>
public sealed record DecisionSubmitRequest
{
    public string Decisions { get; init; } = string.Empty;
}
