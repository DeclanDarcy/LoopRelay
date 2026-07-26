using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;

namespace LoopRelay.Orchestration.Storage;

public enum StorageHealth
{
    Healthy,
    ActionRequired,
    Unsupported,
    Corrupt,
}

public enum StorageOperationKind
{
    Initialize,
    Migrate,
    Export,
    Sync,
}

public enum StorageOperationLifecycle
{
    Planned,
    Effecting,
    Verified,
    Completed,
    RecoveryRequired,
    Refused,
}

public readonly record struct StorageOperationIdentity(string Value)
{
    public static StorageOperationIdentity New() => new(CausalUlid.NewId("storageop"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

/// <summary>
/// How much work a storage verification is authorized to do.
/// </summary>
public enum StorageVerificationDepth
{
    /// <summary>
    /// Per-observation health. Enumerates the persistence tree by name only, hashes the database
    /// file once, answers the schema from <see cref="LoopRelayWorkspaceDatabase.InspectStampedAsync"/>,
    /// and still reads interrupted journal artifacts and interrupted rows. Skips the deep checks:
    /// no digest for the other persistence files, no <c>PRAGMA foreign_key_check</c>, and no
    /// ~190-probe structural classification while the stamp is well-formed.
    /// </summary>
    Light,

    /// <summary>
    /// Everything <see cref="Light"/> does, plus a SHA-256 for every file under the persistence
    /// directory, the full structural classification, and <c>PRAGMA foreign_key_check</c>. This is
    /// what the explicit <c>storage</c> commands verify with, and the default for an unqualified
    /// request.
    /// </summary>
    Deep,
}

/// <summary>
/// One file under the workspace persistence directory. <paramref name="Sha256"/> is null when the
/// entry came from a <see cref="StorageVerificationDepth.Light"/> verification, which lists the
/// tree by name (for journal-artifact detection) without digesting its contents.
/// </summary>
public sealed record StorageTreeEntry(string RelativePath, long Length, string? Sha256);

public sealed record StorageInspection(
    StorageHealth Health,
    bool Exists,
    long? ByteLength,
    string? ByteSha256,
    WorkspaceSchemaInspection? Schema,
    IReadOnlyList<StorageTreeEntry> PersistenceTree,
    IReadOnlyList<string> UnresolvedReferences,
    IReadOnlyList<string> InterruptedOperations,
    IReadOnlyList<string> RequiredActions,
    IReadOnlyList<string> Evidence);

public sealed record StorageVerifyRequest(
    string RepositoryPath,
    StorageVerificationDepth Depth = StorageVerificationDepth.Deep);
public sealed record StorageInitRequest(
    string RepositoryPath,
    WorkspaceIdentity IntendedWorkspace,
    CanonicalCausalContext Causality);
public sealed record StorageMigrateRequest(
    string RepositoryPath,
    CanonicalCausalContext Causality);
public sealed record StorageExportRequest(
    string RepositoryPath,
    string TargetRelativePath,
    CanonicalCausalContext Causality);
public sealed record StorageSyncRequest(
    string RepositoryPath,
    CanonicalCausalContext Causality);

public sealed record StorageOperationPlan(
    StorageOperationIdentity Identity,
    StorageOperationKind Kind,
    CanonicalCausalContext Causality,
    string SourceFingerprint,
    string TargetManifest,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Postconditions,
    string SemanticIdempotencyKey,
    DateTimeOffset PlannedAt);

public sealed record StorageOperationEvent(
    StorageOperationIdentity Operation,
    StorageOperationLifecycle Lifecycle,
    string Explanation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record StorageOperationReceipt(
    StorageOperationIdentity Operation,
    string ObservedFingerprint,
    IReadOnlyList<EffectReceiptIdentity> EffectReceipts,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record StorageOperationResult(
    StorageOperationIdentity? Operation,
    StorageOperationLifecycle Lifecycle,
    StorageInspection Inspection,
    IReadOnlyList<EffectIntentIdentity> Effects,
    string Explanation,
    IReadOnlyList<string> Evidence);

public interface IWorkspaceStorageInspector
{
    Task<StorageInspection> VerifyAsync(StorageVerifyRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkspaceStorageOperationStore
{
    Task PersistPlanAsync(StorageOperationPlan plan, CancellationToken cancellationToken = default);
    Task AppendEventAsync(StorageOperationEvent storageEvent, CancellationToken cancellationToken = default);
    Task PersistReceiptAsync(StorageOperationReceipt receipt, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StorageOperationPlan>> ReadInterruptedAsync(CancellationToken cancellationToken = default);
}

public interface IWorkspaceStorageAuthority
{
    Task<StorageOperationResult> InitializeAsync(StorageInitRequest request, CancellationToken cancellationToken = default);
    Task<StorageOperationResult> MigrateAsync(StorageMigrateRequest request, CancellationToken cancellationToken = default);
    Task<StorageOperationResult> ExportAsync(StorageExportRequest request, CancellationToken cancellationToken = default);
    Task<StorageOperationResult> SyncAsync(StorageSyncRequest request, CancellationToken cancellationToken = default);
}
