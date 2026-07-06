using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record ResolveDecisionCommand(
    string? Rationale,
    string? Resolver,
    string? SelectedOptionId,
    DecisionOutcome Outcome = DecisionOutcome.Accepted,
    string? ExpectedProposalFingerprint = null,
    string? ExpectedPackageId = null,
    string? ExpectedPackageFingerprint = null,
    bool AcknowledgeStaleAuthority = false);
