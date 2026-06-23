using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionInfluenceTrace(
    string Id,
    Guid RepositoryId,
    Guid ExecutionSessionId,
    DateTimeOffset RecordedAt,
    DateTimeOffset ProjectionGeneratedAt,
    string ProjectionFingerprint,
    IReadOnlyList<DecisionInfluenceStatement> Statements,
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
