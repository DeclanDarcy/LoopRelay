namespace CommandCenter.Orchestration.Models;

/// <summary>
/// Payload of <c>GET /api/repositories/{id}/plan/status</c>. <see cref="PlanExists"/> is the
/// primary Plan Authoring gate (<c>false</c> drives the authoring screen); <see cref="State"/>
/// is the human-facing lifecycle projection.
/// </summary>
public sealed record PlanStatus(bool PlanExists, PlanLifecycleState State);
