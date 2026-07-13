using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Effects;

public readonly record struct EffectIntentIdentity(string Value)
{
    public static EffectIntentIdentity New() => new(CausalUlid.NewId("effect"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct EffectReceiptIdentity(string Value)
{
    public static EffectReceiptIdentity New() => new(CausalUlid.NewId("effectreceipt"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public readonly record struct EffectExecutorKey(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public static class TransitionalFeatureEffectExecutorKeys
{
    public static EffectExecutorKey For(string effectIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effectIdentity);
        return new($"canonical-transition-effect:{effectIdentity}");
    }
}

public readonly record struct EffectReconciliationIdentity(string Value)
{
    public static EffectReconciliationIdentity New() => new(CausalUlid.NewId("effectreconciliation"));
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
    public override string ToString() => Value;
}

public enum EffectRequiredness
{
    BlockingLocal,
    RequiredAsync,
}

public enum EffectLifecycle
{
    Planned,
    Leased,
    Started,
    Pending,
    Succeeded,
    Failed,
    Stalled,
    Cancelled,
    Unknown,
    Reconciling,
    RetryAuthorized,
    HumanActionRequired,
}

public enum EffectReconciliationVerdict
{
    Succeeded,
    NotApplied,
    StillUnknown,
    HumanActionRequired,
}

public static class EffectLifecyclePolicy
{
    public static bool CanTransition(EffectLifecycle current, EffectLifecycle next) => (current, next) switch
    {
        (EffectLifecycle.Planned, EffectLifecycle.Leased) => true,
        (EffectLifecycle.Leased, EffectLifecycle.Started or EffectLifecycle.Planned or EffectLifecycle.Unknown or EffectLifecycle.Reconciling) => true,
        (EffectLifecycle.Started, EffectLifecycle.Pending or EffectLifecycle.Succeeded or EffectLifecycle.Failed or
            EffectLifecycle.Stalled or EffectLifecycle.Cancelled or EffectLifecycle.Unknown) => true,
        (EffectLifecycle.Pending, EffectLifecycle.Leased or EffectLifecycle.Succeeded or EffectLifecycle.Failed or
            EffectLifecycle.Stalled or EffectLifecycle.Cancelled or EffectLifecycle.Unknown) => true,
        (EffectLifecycle.Unknown, EffectLifecycle.Leased or EffectLifecycle.Reconciling) => true,
        (EffectLifecycle.Reconciling, EffectLifecycle.Succeeded or EffectLifecycle.Failed or EffectLifecycle.Stalled or
            EffectLifecycle.RetryAuthorized or EffectLifecycle.HumanActionRequired or EffectLifecycle.Unknown) => true,
        (EffectLifecycle.Failed or EffectLifecycle.Stalled, EffectLifecycle.RetryAuthorized) => true,
        (EffectLifecycle.RetryAuthorized, EffectLifecycle.Leased) => true,
        _ => false,
    };

    public static void RequireTransition(EffectLifecycle current, EffectLifecycle next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException($"Illegal effect lifecycle transition: {current} -> {next}.");
        }
    }
}

public sealed record EffectTargetDescriptor(string Kind, string Identity, string Document)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(Document);
    }
}

public sealed record EffectCondition(string Kind, string Document)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(Document);
    }
}

public static class GitEffectExecutorKeys
{
    public static EffectExecutorKey NestedRepositoryCommit { get; } = new("git-nested-commit");
    public static EffectExecutorKey NestedRepositoryPush { get; } = new("git-nested-push");
    public static EffectExecutorKey ParentGitlinkCommit { get; } = new("git-parent-gitlink-commit");
    public static EffectExecutorKey ParentWorkingTreeCommit { get; } = new("git-parent-worktree-commit");
    public static EffectExecutorKey ParentRepositoryPush { get; } = new("git-parent-push");
}

public static class WorkspaceEffectExecutorKeys
{
    public static EffectExecutorKey CheckpointCleanup { get; } = new("workspace-checkpoint-cleanup");
    public static EffectExecutorKey DecisionContinuityCleanup { get; } = new("workspace-decision-continuity-cleanup");
    public static EffectExecutorKey FilesystemWrite { get; } = new("workspace-filesystem-write");
    public static EffectExecutorKey SurfaceRestore { get; } = new("workspace-surface-restore");
    public static EffectExecutorKey RotateLiveHandoff { get; } = new("workspace-rotate-live-handoff");
    public static EffectExecutorKey RetireLiveDecisions { get; } = new("workspace-retire-live-decisions");
    public static EffectExecutorKey RotateOperationalDelta { get; } = new("workspace-rotate-operational-delta");
    public static EffectExecutorKey ExportPackageWrite { get; } = new("workspace-export-package-write");
    public static EffectExecutorKey CompletionArchive { get; } = new("workspace-completion-archive");
    public static EffectExecutorKey StorageAuthorityPromotion { get; } = new("workspace-storage-authority-promotion");
    public static EffectExecutorKey ImportAuthorityPromotion { get; } = new("workspace-import-authority-promotion");
}

public sealed record WorkspaceCheckpointCleanupPayload(IReadOnlyList<string> MetadataKeys);
public sealed record DecisionContinuityCleanupPayload(string CausalReference);
public sealed record FilesystemWriteEffectPayload(string RelativePath, string Content);
public sealed record SurfaceRestoreFile(string RelativePath, string Content, string Sha256);
public sealed record SurfaceRestoreEffectPayload(
    IReadOnlyList<SurfaceRestoreFile> Files,
    IReadOnlyList<string> DeletePaths,
    string ManifestHash);
public sealed record LoopArtifactRotationEffectPayload(
    string Operation,
    string SourceRelativePath,
    string ExpectedContentHash);
public sealed record ExportPackageEffectPayload(
    string TargetRelativePath,
    string Base64Content,
    string ContentSha256);
public sealed record CompletionArchiveEffectPayload(
    string ActiveEpicPath,
    string ArchiveRoot,
    int Index,
    string ArchiveDirectory,
    string SynthesisPath);
public sealed record StorageAuthorityPromotionEffectPayload(
    string SourceRelativePath,
    string TargetRelativePath,
    string ExpectedSha256);
public sealed record ImportAuthorityPromotionEffectPayload(
    string SourceRelativePath,
    string TargetRelativePath,
    string? ExistingAuthorityArchiveRelativePath,
    string ExpectedSha256);

public sealed record GitEffectPayload(
    string RepositoryRelativeWorkingDirectory,
    string CommitMessage,
    string? Pathspec = null);

public sealed record EffectIntent
{
    public EffectIntent(
        EffectIntentIdentity identity,
        CanonicalCausalContext causality,
        string semanticOperationKey,
        EffectExecutorKey executor,
        string executorVersion,
        EffectTargetDescriptor target,
        string typedPayload,
        string typedPayloadHash,
        int order,
        IReadOnlyList<EffectIntentIdentity> dependencies,
        EffectRequiredness requiredness,
        EffectCondition precondition,
        EffectCondition postcondition,
        string reconciliationPolicy,
        string idempotencyKey,
        DateTimeOffset plannedAt)
    {
        ArgumentNullException.ThrowIfNull(causality);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(precondition);
        ArgumentNullException.ThrowIfNull(postcondition);
        if (identity.IsEmpty || executor.IsEmpty || causality.Workspace.IsEmpty ||
            causality.Run.IsEmpty || causality.WorkflowInstance.IsEmpty ||
            causality.TransitionRun.IsEmpty || causality.Attempt.IsEmpty || order < 0)
        {
            throw new ArgumentException("Effect identity, causality, executor, and order must be valid.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(semanticOperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(executorVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(typedPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(typedPayloadHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(reconciliationPolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        target.Validate();
        precondition.Validate();
        postcondition.Validate();
        if (dependencies.Any(item => item.IsEmpty || item == identity) ||
            dependencies.Distinct().Count() != dependencies.Count)
        {
            throw new ArgumentException("Effect dependencies must be unique, non-empty, and cannot reference the intent itself.");
        }

        Identity = identity;
        Causality = causality;
        SemanticOperationKey = semanticOperationKey;
        Executor = executor;
        ExecutorVersion = executorVersion;
        Target = target;
        TypedPayload = typedPayload;
        TypedPayloadHash = typedPayloadHash;
        Order = order;
        Dependencies = dependencies;
        Requiredness = requiredness;
        Precondition = precondition;
        Postcondition = postcondition;
        ReconciliationPolicy = reconciliationPolicy;
        IdempotencyKey = idempotencyKey;
        PlannedAt = plannedAt;
    }

    public EffectIntentIdentity Identity { get; }
    public CanonicalCausalContext Causality { get; }
    public string SemanticOperationKey { get; }
    public EffectExecutorKey Executor { get; }
    public string ExecutorVersion { get; }
    public EffectTargetDescriptor Target { get; }
    public string TypedPayload { get; }
    public string TypedPayloadHash { get; }
    public int Order { get; }
    public IReadOnlyList<EffectIntentIdentity> Dependencies { get; }
    public EffectRequiredness Requiredness { get; }
    public EffectCondition Precondition { get; }
    public EffectCondition Postcondition { get; }
    public string ReconciliationPolicy { get; }
    public string IdempotencyKey { get; }
    public DateTimeOffset PlannedAt { get; }
}

public sealed record EffectReceipt(
    EffectReceiptIdentity Identity,
    EffectIntentIdentity Intent,
    EffectExecutorKey Executor,
    string ExecutorVersion,
    string ObservedTargetIdentity,
    string BeforeFacts,
    string AfterFacts,
    bool PostconditionSatisfied,
    string? ExternalCorrelation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record EffectLifecycleEvent(
    long Sequence,
    EffectIntentIdentity Intent,
    EffectLifecycle State,
    string Worker,
    string Explanation,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record EffectWorkItem(
    EffectIntent Intent,
    EffectLifecycle State,
    long RowVersion,
    string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt,
    int AttemptCount,
    EffectReceipt? Receipt,
    IReadOnlyList<EffectLifecycleEvent> Events);

public sealed record EffectLease(
    EffectIntent Intent,
    long RowVersion,
    string Worker,
    DateTimeOffset ExpiresAt,
    EffectLifecycle PreviousState);

public sealed record EffectExecutionObservation(
    EffectLifecycle State,
    string Explanation,
    IReadOnlyList<string> Evidence,
    string BeforeFacts,
    string AfterFacts,
    bool PostconditionSatisfied,
    string? ExternalCorrelation = null);

public sealed record EffectReconciliationObservation(
    EffectReconciliationVerdict Verdict,
    string Explanation,
    IReadOnlyList<string> Evidence,
    string BeforeFacts,
    string AfterFacts,
    string? ExternalCorrelation = null);

public interface IEffectWorkStore
{
    Task<IReadOnlyList<EffectWorkItem>> ScanUnsettledAsync(int limit, DateTimeOffset now, CancellationToken cancellationToken);
    Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(TransitionRunIdentity transitionRun, CancellationToken cancellationToken);
    Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken cancellationToken);
    Task<EffectLease?> TryLeaseAsync(EffectIntentIdentity identity, long expectedRowVersion, string worker, DateTimeOffset now, TimeSpan duration, CancellationToken cancellationToken);
    Task<EffectWorkItem> AppendLifecycleAsync(EffectIntentIdentity identity, long expectedRowVersion, EffectLifecycle state, string worker, string explanation, IReadOnlyList<string> evidence, DateTimeOffset recordedAt, CancellationToken cancellationToken);
    Task<EffectWorkItem> RecordReceiptAsync(EffectIntentIdentity identity, long expectedRowVersion, EffectReceipt receipt, string worker, CancellationToken cancellationToken);
    Task RecordReconciliationAsync(EffectIntentIdentity identity, long expectedRowVersion, EffectReconciliationObservation observation, string worker, DateTimeOffset recordedAt, CancellationToken cancellationToken);
}

public interface IEffectPlanStore
{
    Task AppendPlanAsync(IReadOnlyList<EffectIntent> intents, CancellationToken cancellationToken);
}

public interface IEffectPlanSettlementStore
{
    Task<bool> TrySettleAsync(TransitionRunIdentity transitionRun, CancellationToken cancellationToken);
    Task RecordOutcomeAsync(TransitionRunIdentity transitionRun, RuntimeOutcomeKind outcome, string explanation, CancellationToken cancellationToken);
}

public interface IEffectExecutor
{
    EffectExecutorKey Key { get; }
    string Version { get; }
    Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken);
}

public interface IEffectExecutorRegistry
{
    IEffectExecutor Resolve(EffectExecutorKey key, string version);
}

public interface IEffectReconciler
{
    Task<EffectReconciliationObservation> ReconcileAsync(EffectIntent intent, CancellationToken cancellationToken);
}

public sealed class EffectReconcilerRegistry(
    IReadOnlyDictionary<EffectExecutorKey, IEffectReconciler> _reconcilers,
    IEffectReconciler _fallback) : IEffectReconciler
{
    public Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken) =>
        (_reconcilers.TryGetValue(intent.Executor, out IEffectReconciler? reconciler)
            ? reconciler
            : _fallback).ReconcileAsync(intent, cancellationToken);
}

public sealed class EffectExecutorRegistry(IEnumerable<IEffectExecutor> executors) : IEffectExecutorRegistry
{
    private readonly IReadOnlyDictionary<(EffectExecutorKey Key, string Version), IEffectExecutor> _executors =
        executors.ToDictionary(item => (item.Key, item.Version));

    public IEffectExecutor Resolve(EffectExecutorKey key, string version) =>
        _executors.TryGetValue((key, version), out IEffectExecutor? executor)
            ? executor
            : throw new InvalidOperationException($"No effect executor is registered for '{key}' version '{version}'.");
}
