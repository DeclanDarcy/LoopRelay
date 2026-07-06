using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionInfluenceTrace(
    string Id,
    Guid RepositoryId,
    Guid ExecutionSessionId,
    DateTimeOffset RecordedAt,
    DateTimeOffset ProjectionGeneratedAt,
    string ProjectionFingerprint,
    IReadOnlyList<DecisionInfluenceStatement> Statements,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IncludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ExcludedDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> SupersededDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> ConflictingDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> IgnoredDecisions,
    IReadOnlyList<DecisionProjectionDecisionDiagnostic> BlockedDecisions,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionInfluenceStatement(
    string StatementId,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    ExecutionProjectionKind ProjectionKind,
    string StatementType,
    string PromptSection,
    int? PriorityRank,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<DecisionAdherenceObservation> AdherenceObservations);

public sealed record DecisionAdherenceObservation(
    DateTimeOffset ObservedAt,
    string Observer,
    string Observation);
