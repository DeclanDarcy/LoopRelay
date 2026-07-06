namespace LoopRelay.Orchestration.Models;

/// <summary>
/// Returned by the write/revise/execute commands. The planning turn runs in the background on the
/// orchestrator's own lifetime; the client observes progress and completion over
/// <c>GET /api/repositories/{id}/plan/stream</c>. <see cref="Phase"/> names the accepted transition
/// (<c>WritePlan</c>, <c>RevisePlan</c>, <c>ExecutePlan</c>).
/// </summary>
public sealed record PlanRunAcknowledgement(string Phase);
