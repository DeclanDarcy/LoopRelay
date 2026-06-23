namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageRegenerationRequest(
    RefinementPlan Plan,
    string BasePackageId,
    string BasePackageFingerprint,
    string? RequestedBy = null);
