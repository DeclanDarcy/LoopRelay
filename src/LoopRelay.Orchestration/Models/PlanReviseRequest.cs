namespace LoopRelay.Orchestration.Models;

/// <summary>
/// Revise Plan request (m3): user feedback submitted to the same held-open planning process via
/// <c>RevisePlan.Render(feedback)</c>. Feedback is required.
/// </summary>
public sealed record PlanReviseRequest
{
    public string Feedback { get; init; } = string.Empty;
}
