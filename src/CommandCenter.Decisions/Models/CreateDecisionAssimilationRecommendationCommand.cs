namespace CommandCenter.Decisions.Models;

public sealed record CreateDecisionAssimilationRecommendationCommand(
    string? RequestedBy = null,
    string? Notes = null);
