using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowDecisionProjection(
    Guid RepositoryId,
    string? DecisionId,
    string? CandidateId,
    string? ProposalId,
    string? PackageId,
    WorkflowDecisionStatus Status,
    string? ReviewState,
    string? ResolutionState,
    string HumanAuthoringBurden,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ResolvedAt,
    bool IsResolutionEligible,
    bool IsGovernanceBlocked,
    string? GovernanceStatus,
    string? QualityStatus,
    string? CertificationStatus,
    string? ReplacementDecisionId,
    WorkflowDecisionDiagnostics Diagnostics);
