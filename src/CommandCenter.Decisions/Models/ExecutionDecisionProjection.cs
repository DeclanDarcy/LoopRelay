using System.Text.Json.Serialization;

namespace CommandCenter.Decisions.Models;

[method: JsonConstructor]
public sealed record ExecutionDecisionProjection(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ExecutionConstraint> Constraints,
    IReadOnlyList<ExecutionDirective> Directives,
    IReadOnlyList<ExecutionDecisionPriority> Priorities,
    IReadOnlyList<ExecutionArchitectureRule> ArchitectureRules,
    IReadOnlyList<ExecutionDecisionConflict> Conflicts,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IncludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ExcludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> SupersededDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ConflictingDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IgnoredDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> BlockedDecisions,
    IReadOnlyList<DecisionProjectedStatement> ProjectedStatements,
    ExecutionDecisionContext Context,
    string ProjectionFingerprint = "")
{
    public ExecutionDecisionProjection(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        IReadOnlyList<ExecutionConstraint> constraints,
        IReadOnlyList<ExecutionDirective> directives,
        IReadOnlyList<ExecutionDecisionPriority> priorities,
        IReadOnlyList<ExecutionArchitectureRule> architectureRules,
        IReadOnlyList<ExecutionDecisionConflict> conflicts,
        IReadOnlyList<string> diagnostics,
        ExecutionDecisionContext context,
        string projectionFingerprint = "")
        : this(
            repositoryId,
            generatedAt,
            constraints,
            directives,
            priorities,
            architectureRules,
            conflicts,
            diagnostics,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            context,
            projectionFingerprint)
    {
    }
}
