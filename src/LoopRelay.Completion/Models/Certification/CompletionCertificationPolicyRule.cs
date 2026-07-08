namespace LoopRelay.Completion.Models.Certification;

public sealed record CompletionCertificationPolicyRule(
    string ClosureRecommendation,
    IReadOnlyList<string> AllowedCompletionStatuses,
    IReadOnlyList<string> AllowedDriftClassifications,
    string Rationale)
{
    public bool AllowsCompletionStatus(string status) =>
        AllowedCompletionStatuses.Contains(status, StringComparer.Ordinal);

    public bool AllowsDriftClassification(string driftClassification) =>
        AllowedDriftClassifications.Contains(driftClassification, StringComparer.Ordinal);
}
