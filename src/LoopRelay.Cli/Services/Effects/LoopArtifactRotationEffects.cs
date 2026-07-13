using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Effects;

internal interface ILoopArtifactEffectCoordinator
{
    Task<string?> RotateLiveHandoffAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken);
    Task<bool> RetireLiveDecisionsAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken);
    Task<string?> RotateOperationalDeltaAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken);
}

internal sealed class DirectLoopArtifactEffectCoordinator(LoopArtifacts _artifacts)
    : ILoopArtifactEffectCoordinator
{
    public Task<string?> RotateLiveHandoffAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _artifacts.RotateLiveHandoffAsync(causality);
    }

    public Task<bool> RetireLiveDecisionsAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _artifacts.RetireLiveDecisionsAsync();
    }

    public Task<string?> RotateOperationalDeltaAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _artifacts.RotateOperationalDeltaAsync(causality);
    }
}

internal sealed class DurableLoopArtifactEffectCoordinator(
    Repository _repository,
    LoopArtifacts _artifacts) : ILoopArtifactEffectCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<string?> RotateLiveHandoffAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken) =>
        ExecuteAsync("RotateLiveHandoff", OrchestrationArtifactPaths.LiveHandoff, causality, cancellationToken);

    public async Task<bool> RetireLiveDecisionsAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken) =>
        await ExecuteAsync("RetireLiveDecisions", OrchestrationArtifactPaths.Decisions, causality, cancellationToken) is not null;

    public Task<string?> RotateOperationalDeltaAsync(
        CanonicalCausalContext causality,
        CancellationToken cancellationToken) =>
        ExecuteAsync("RotateOperationalDelta", OrchestrationArtifactPaths.OperationalDelta, causality, cancellationToken);

    private async Task<string?> ExecuteAsync(
        string operation,
        string sourceRelativePath,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        string? content = await _artifacts.ReadAsync(sourceRelativePath);
        if (content is null) return null;
        var store = new CanonicalEffectWorkStore(_repository);
        IReadOnlyList<EffectWorkItem> existingPlan = await store.ReadPlanAsync(
            causality.TransitionRun, cancellationToken);
        EffectWorkItem? parent = existingPlan
            .Where(item => item.State == EffectLifecycle.Started &&
                item.Intent.Causality.Attempt == causality.Attempt &&
                !IsLoopArtifactExecutor(item.Intent.Executor))
            .OrderByDescending(item => item.Intent.Order)
            .FirstOrDefault();
        string contentHash = LoopHistoryRecord.ComputeContentHash(content);
        var payload = new LoopArtifactRotationEffectPayload(
            operation,
            sourceRelativePath,
            contentHash);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string occurrence = causality.Turn?.Value ?? causality.Attempt.Value;
        var intent = new EffectIntent(
            EffectIntentIdentity.New(), causality,
            $"loop-artifact:{operation}",
            ExecutorFor(operation),
            "1",
            new EffectTargetDescriptor("LoopArtifact", sourceRelativePath,
                JsonSerializer.Serialize(new { operation, occurrence }, JsonOptions)),
            payloadJson,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))),
            parent?.Intent.Order + 1 ?? 0,
            parent is null ? [] : [parent.Intent.Identity],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("source-content-hash", JsonSerializer.Serialize(new { contentHash }, JsonOptions)),
            new EffectCondition("history-durable-source-retired", JsonSerializer.Serialize(new { contentHash }, JsonOptions)),
            "loop-history-and-source-observation",
            $"loop-artifact:{operation}:{causality.TransitionRun.Value}:{occurrence}:{contentHash}",
            DateTimeOffset.UtcNow);
        await store.AppendPlanAsync([intent], cancellationToken);
        if (parent is not null)
        {
            return content;
        }
        IEffectExecutor rotationExecutor = ExecutorFor(operation) switch
        {
            var key when key == WorkspaceEffectExecutorKeys.RotateLiveHandoff =>
                new LiveHandoffRotationEffectExecutor(_artifacts),
            var key when key == WorkspaceEffectExecutorKeys.RetireLiveDecisions =>
                new LiveDecisionRetirementEffectExecutor(_artifacts),
            _ => new OperationalDeltaRotationEffectExecutor(_artifacts),
        };
        var filesystemExecutor = new FilesystemWriteEffectExecutor(_repository);
        var rotationReconciler = new LoopArtifactRotationEffectReconciler(_repository, _artifacts);
        var filesystemReconciler = new FilesystemWriteEffectReconciler(_repository);
        var worker = new EffectWorker(
            $"loop-artifact-{Environment.ProcessId}",
            store,
            new EffectExecutorRegistry([rotationExecutor, filesystemExecutor]),
            new EffectReconcilerRegistry(
                new Dictionary<EffectExecutorKey, IEffectReconciler>
                {
                    [rotationExecutor.Key] = rotationReconciler,
                    [WorkspaceEffectExecutorKeys.FilesystemWrite] = filesystemReconciler,
                },
                rotationReconciler),
            TimeSpan.FromMinutes(2));
        await worker.RunOnceAsync(cancellationToken, only: new HashSet<EffectIntentIdentity> { intent.Identity });
        EffectWorkItem rotation = await store.ReadAsync(intent.Identity, cancellationToken)
            ?? throw new InvalidOperationException("Loop-artifact rotation intent disappeared.");
        if (rotation.State != EffectLifecycle.Succeeded || rotation.Receipt is not { PostconditionSatisfied: true })
        {
            throw new InvalidOperationException(
                $"Operational-delta rotation did not produce a verified receipt; current state is {rotation.State}.");
        }

        IReadOnlyList<EffectWorkItem> plan = await store.ReadPlanAsync(causality.TransitionRun, cancellationToken);
        HashSet<EffectIntentIdentity> projections = plan
            .Where(item => item.State == EffectLifecycle.Planned &&
                item.Intent.Executor == WorkspaceEffectExecutorKeys.FilesystemWrite &&
                item.Intent.Dependencies.Contains(intent.Identity))
            .Select(item => item.Intent.Identity)
            .ToHashSet();
        if (projections.Count > 0)
        {
            await worker.RunOnceAsync(cancellationToken, includePending: false, only: projections);
        }
        return content;
    }

    private static EffectExecutorKey ExecutorFor(string operation) => operation switch
    {
        "RotateLiveHandoff" => WorkspaceEffectExecutorKeys.RotateLiveHandoff,
        "RetireLiveDecisions" => WorkspaceEffectExecutorKeys.RetireLiveDecisions,
        "RotateOperationalDelta" => WorkspaceEffectExecutorKeys.RotateOperationalDelta,
        _ => throw new InvalidOperationException($"Unsupported loop-artifact operation '{operation}'."),
    };

    private static bool IsLoopArtifactExecutor(EffectExecutorKey key) =>
        key == WorkspaceEffectExecutorKeys.RotateLiveHandoff ||
        key == WorkspaceEffectExecutorKeys.RetireLiveDecisions ||
        key == WorkspaceEffectExecutorKeys.RotateOperationalDelta;
}

internal abstract class LoopArtifactRotationEffectExecutorBase : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    protected LoopArtifactRotationEffectExecutorBase(LoopArtifacts artifacts) => Artifacts = artifacts;
    protected LoopArtifacts Artifacts { get; }
    public abstract EffectExecutorKey Key { get; }
    protected abstract string Operation { get; }
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        LoopArtifactRotationEffectPayload payload = Parse(intent);
        if (!string.Equals(payload.Operation, Operation, StringComparison.Ordinal))
            throw new InvalidOperationException($"Executor '{Key}' cannot perform '{payload.Operation}'.");
        cancellationToken.ThrowIfCancellationRequested();
        string? source = await Artifacts.ReadAsync(payload.SourceRelativePath);
        if (source is null)
        {
            return new EffectExecutionObservation(
                EffectLifecycle.Unknown,
                "The planned operational delta was absent at execution time.",
                [payload.SourceRelativePath], payload.ExpectedContentHash, "missing", false);
        }
        string beforeHash = LoopHistoryRecord.ComputeContentHash(source);
        if (!string.Equals(beforeHash, payload.ExpectedContentHash, StringComparison.Ordinal))
        {
            return new EffectExecutionObservation(
                EffectLifecycle.Failed,
                "Loop-artifact source changed after the intent was planned.",
                [payload.SourceRelativePath, beforeHash], payload.ExpectedContentHash, beforeHash, false);
        }

        bool mutated = await MutateAsync(intent.Causality);
        bool satisfied = mutated;
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? $"Loop-artifact operation '{payload.Operation}' completed."
                : $"Loop-artifact operation '{payload.Operation}' did not mutate its planned source.",
            [payload.SourceRelativePath, beforeHash], payload.ExpectedContentHash,
            satisfied ? "source-retired" : "source-present", satisfied, beforeHash);
    }

    internal static LoopArtifactRotationEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<LoopArtifactRotationEffectPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Loop-artifact rotation payload is invalid.");

    protected abstract Task<bool> MutateAsync(CanonicalCausalContext causality);
}

internal sealed class LiveHandoffRotationEffectExecutor(LoopArtifacts artifacts)
    : LoopArtifactRotationEffectExecutorBase(artifacts)
{
    public override EffectExecutorKey Key => WorkspaceEffectExecutorKeys.RotateLiveHandoff;
    protected override string Operation => "RotateLiveHandoff";
    protected override async Task<bool> MutateAsync(CanonicalCausalContext causality) =>
        await Artifacts.RotateLiveHandoffAsync(causality) is not null;
}

internal sealed class LiveDecisionRetirementEffectExecutor(LoopArtifacts artifacts)
    : LoopArtifactRotationEffectExecutorBase(artifacts)
{
    public override EffectExecutorKey Key => WorkspaceEffectExecutorKeys.RetireLiveDecisions;
    protected override string Operation => "RetireLiveDecisions";
    protected override Task<bool> MutateAsync(CanonicalCausalContext causality) =>
        Artifacts.RetireLiveDecisionsAsync();
}

internal sealed class OperationalDeltaRotationEffectExecutor(LoopArtifacts artifacts)
    : LoopArtifactRotationEffectExecutorBase(artifacts)
{
    public override EffectExecutorKey Key => WorkspaceEffectExecutorKeys.RotateOperationalDelta;
    protected override string Operation => "RotateOperationalDelta";
    protected override async Task<bool> MutateAsync(CanonicalCausalContext causality) =>
        await Artifacts.RotateOperationalDeltaAsync(causality) is not null;
}

internal sealed class LoopArtifactRotationEffectReconciler(
    Repository _repository,
    LoopArtifacts _artifacts) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        LoopArtifactRotationEffectPayload payload = LoopArtifactRotationEffectExecutorBase.Parse(intent);
        bool sourceExists = await _artifacts.ExistsAsync(payload.SourceRelativePath);
        LoopHistoryKind? kind = payload.Operation switch
        {
            "RotateLiveHandoff" => LoopHistoryKind.Handoff,
            "RotateOperationalDelta" => LoopHistoryKind.OperationalDelta,
            "RetireLiveDecisions" => null,
            _ => throw new InvalidOperationException($"Unsupported loop-artifact operation '{payload.Operation}'."),
        };
        LoopHistoryRecord? latest = kind is null
            ? null
            : await new LedgerLoopHistoryStore(_repository).ReadLatestAsync(kind.Value, cancellationToken);
        bool recommendationExists = payload.Operation == "RetireLiveDecisions" &&
            await _artifacts.ExistsAsync(OrchestrationArtifactPaths.ExecutionRecommendation);
        bool satisfied = payload.Operation == "RetireLiveDecisions"
            ? !sourceExists && !recommendationExists
            : !sourceExists && latest is not null &&
                string.Equals(latest.ContentHash, payload.ExpectedContentHash, StringComparison.Ordinal);
        return satisfied
            ? new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Canonical history/projection facts and source absence independently confirm the loop-artifact operation.",
                latest is null
                    ? [payload.SourceRelativePath]
                    : [latest.Identity.Value, latest.MaterializedRelativePath ?? "projection-unmaterialized"],
                payload.ExpectedContentHash, latest?.ContentHash ?? "source-retired", latest?.Identity.Value)
            : new EffectReconciliationObservation(
                EffectReconciliationVerdict.NotApplied,
                "Operational-delta rotation postconditions are not both satisfied.",
                [payload.SourceRelativePath], payload.ExpectedContentHash,
                latest?.ContentHash ?? (sourceExists ? "source-present" : "history-missing"));
    }
}
