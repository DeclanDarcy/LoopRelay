namespace LoopRelay.Decisions.Models;

public sealed record DecisionEvidenceInspection(
    string ProposalId,
    string CandidateId,
    IReadOnlyList<DecisionEvidenceInspectionItem> Items,
    DecisionReviewDiagnostics Diagnostics);
