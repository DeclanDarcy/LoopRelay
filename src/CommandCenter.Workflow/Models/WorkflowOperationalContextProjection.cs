using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowOperationalContextProjection(
    Guid RepositoryId,
    string? ProposalId,
    WorkflowOperationalContextStatus Status,
    string? ReviewState,
    string? PromotionState,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset? PromotedAt,
    string? Reviewer,
    string Summary,
    string? SourceDecisionId,
    string? SourceExecutionId,
    bool IsReviewEligible,
    bool IsPromotionEligible,
    bool IsCommitEligible,
    WorkflowOperationalContextDiagnostics Diagnostics);
