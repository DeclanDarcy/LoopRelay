namespace CommandCenter.Decisions.Models;

public sealed record DecisionContextSnapshot(
    string SnapshotId,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    string Fingerprint,
    DecisionContext Context,
    DecisionContextDiagnostics Diagnostics,
    DecisionContextValidationResult Validation);
