namespace CommandCenter.Orchestration.Models;

/// <summary>
/// Write Plan request (m3). The Roadmap is required; Specs are unbounded; NewCodebase selects
/// <c>WritePlanForNewCodebase</c> vs <c>WritePlanAgainstCodebase</c> on the backend. The UI never
/// composes prompt text or selects a prompt class — it sends these inputs and the orchestrator
/// renders from <c>CommandCenter.Core.Prompts</c>.
/// </summary>
public sealed record PlanWriteRequest
{
    public string Roadmap { get; init; } = string.Empty;

    public IReadOnlyList<string> Specs { get; init; } = Array.Empty<string>();

    public bool NewCodebase { get; init; }
}
