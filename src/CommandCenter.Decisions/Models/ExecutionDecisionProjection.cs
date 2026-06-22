namespace CommandCenter.Decisions.Models;

public sealed record ExecutionDecisionProjection(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExecutionConstraint> Constraints,
    IReadOnlyList<ExecutionDirective> Directives,
    IReadOnlyList<ExecutionDecisionConflict> Conflicts,
    IReadOnlyList<string> Diagnostics);
