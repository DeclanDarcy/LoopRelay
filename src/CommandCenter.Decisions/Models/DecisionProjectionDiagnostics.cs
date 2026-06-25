using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionProjectionDiagnostics(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string ProjectionFingerprint,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IncludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ExcludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> SupersededDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ConflictingDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IgnoredDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> BlockedDecisions,
    IReadOnlyList<DecisionProjectedStatement> ProjectedStatements,
    IReadOnlyList<ExecutionDecisionConflict> Conflicts,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionProjectionDecisionDiagnostic(
    string DecisionId,
    string Title,
    DecisionState State,
    DecisionOutcome? Outcome,
    DecisionClassification Classification,
    string Reason,
    IReadOnlyList<string> ProjectedStatementIds);

public sealed record DecisionProjectedStatement(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    ExecutionProjectionKind ProjectionKind,
    string ProjectionCategory,
    IReadOnlyList<DecisionSourceReference> Sources);
