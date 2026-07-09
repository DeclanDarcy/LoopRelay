namespace LoopRelay.Core.Abstractions.Artifacts;

public enum LogicalArtifactDomain
{
    Unknown,
    RetainedFile,
    DecisionLedger,
    RoadmapState,
    ArtifactLifecycle,
    SplitLineage,
    ExecutionPreparationManifest,
    SelectionProvenanceManifest,
    ProjectionManifest,
    TransitionJournal,
    LoopHistory,
    ExecutionEvidence,
    ProjectionBody,
    CompletedEpicArchive,
}

public enum LogicalArtifactStorageKind
{
    Unknown,
    RetainedFilesystem,
    FileBackedMigratedDomain,
    SqliteCanonicalRecord,
}

public enum LogicalArtifactResolutionStatus
{
    Resolved,
    MissingRetainedFile,
    MissingMigratedRecord,
    WrongDomain,
    InvalidPath,
    Stale,
    Invalid,
    Blocked,
}

public sealed record LogicalArtifactDescriptor(
    string RelativePath,
    LogicalArtifactDomain Domain,
    LogicalArtifactStorageKind StorageKind,
    string Identity = "");

public sealed record LogicalArtifactContent(string Text);

public sealed record LogicalArtifactPathPattern(
    string Directory,
    string SearchPattern,
    LogicalArtifactDomain Domain,
    string IdentityPrefix = "");

public sealed record LogicalArtifactResolutionResult(
    LogicalArtifactDescriptor Descriptor,
    LogicalArtifactResolutionStatus Status,
    LogicalArtifactContent? Content = null,
    string? Message = null)
{
    public bool IsResolved => Status == LogicalArtifactResolutionStatus.Resolved;

    public static LogicalArtifactResolutionResult Resolved(LogicalArtifactDescriptor descriptor, string content) =>
        new(descriptor, LogicalArtifactResolutionStatus.Resolved, new LogicalArtifactContent(content));

    public static LogicalArtifactResolutionResult Unresolved(
        LogicalArtifactDescriptor descriptor,
        LogicalArtifactResolutionStatus status,
        string message) =>
        new(descriptor, status, null, message);
}

public sealed record CanonicalArtifactHash(
    LogicalArtifactDescriptor Descriptor,
    string Algorithm,
    string Value);

public interface ILogicalArtifactProvider
{
    bool CanResolve(string relativePath);

    Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

public interface ILogicalArtifactResolver
{
    Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

public interface ICanonicalArtifactHasher
{
    Task<CanonicalArtifactHash?> HashIfPresentAsync(
        string relativePath,
        CancellationToken cancellationToken = default);

    Task<CanonicalArtifactHash> RequireHashAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
