namespace CommandCenter.Workflow.Models;

public sealed record WorkflowRecoveryDiagnostics(
    Guid RepositoryId,
    DateTimeOffset RecoveredAt,
    string DomainFingerprint,
    string? PersistedFingerprint,
    bool Rebuilt,
    bool PersistedEvidenceMatchedDomain,
    IReadOnlyList<string> RecoveredArtifacts,
    IReadOnlyList<string> DiscardedArtifacts,
    IReadOnlyList<string> Diagnostics);
