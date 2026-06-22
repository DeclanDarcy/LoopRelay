using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionLifecycleCertificationResult(
    DecisionLifecycleCertificationResultKind Kind,
    int PassedEvidenceCount,
    int FailedEvidenceCount);
