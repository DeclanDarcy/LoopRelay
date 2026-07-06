using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionLifecycleCertificationResult(
    DecisionLifecycleCertificationResultKind Kind,
    int PassedEvidenceCount,
    int FailedEvidenceCount);
