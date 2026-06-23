namespace CommandCenter.Decisions.Models;

public sealed record ExecutionDecisionProjection(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExecutionConstraint> Constraints,
    IReadOnlyList<ExecutionDirective> Directives,
    IReadOnlyList<ExecutionDecisionPriority> Priorities,
    IReadOnlyList<ExecutionArchitectureRule> ArchitectureRules,
    IReadOnlyList<ExecutionDecisionConflict> Conflicts,
    IReadOnlyList<string> Diagnostics,
    ExecutionDecisionContext Context,
    string ProjectionFingerprint = "");
