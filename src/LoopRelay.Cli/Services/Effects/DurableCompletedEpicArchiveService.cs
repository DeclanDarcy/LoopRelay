using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Cli.Services.Effects;

internal sealed class DurableCompletedEpicArchiveService(
    Repository _repository,
    CanonicalCausalContext _causality,
    IArtifactStore _store,
    ICompletedEpicArchiveService _inner) : ICompletedEpicArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var artifacts = new CompletionArtifacts(_store, _repository);
        int index = (await artifacts.ListDirectoriesAsync(request.ArchiveRoot)).Count + 1;
        string archiveDirectory = $"{request.ArchiveRoot}/{index}";
        string synthesisPath = $"{request.ArchiveRoot}/{index}.md";
        var payload = new CompletionArchiveEffectPayload(
            request.ActiveEpicPath, request.ArchiveRoot, index, archiveDirectory, synthesisPath);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        string idempotencyKey = $"completion-archive:{_causality.TransitionRun.Value}:{index}:{payloadHash}";
        var candidate = new EffectIntent(
            EffectIntentIdentity.New(), _causality, "completion:archive-and-synthesize",
            WorkspaceEffectExecutorKeys.CompletionArchive, "1",
            new EffectTargetDescriptor("CompletedEpicArchive", archiveDirectory, payloadJson),
            payloadJson, payloadHash, 0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("archive-targets-absent", payloadJson),
            new EffectCondition("archive-and-synthesis-present", payloadJson),
            "archive-structure-and-synthesis-observation", idempotencyKey, DateTimeOffset.UtcNow);
        var workStore = new CanonicalEffectWorkStore(_repository);
        await workStore.AppendPlanAsync([candidate], cancellationToken);
        EffectWorkItem intent = (await workStore.ReadBySemanticOperationAsync(
                "completion:archive-and-synthesize", cancellationToken))
            .Single(item => item.Intent.IdempotencyKey == idempotencyKey);
        var executor = new CompletionArchiveEffectExecutor(_inner, request);
        var reconciler = new CompletionArchiveEffectReconciler(_repository, _store);
        var worker = new EffectWorker(
            $"completion-archive-{Environment.ProcessId}", workStore,
            new EffectExecutorRegistry([executor]), reconciler, TimeSpan.FromMinutes(5));
        for (int pass = 0; pass < 3; pass++)
        {
            await worker.RunOnceAsync(
                cancellationToken, includePending: true,
                only: new HashSet<EffectIntentIdentity> { intent.Intent.Identity });
            intent = await workStore.ReadAsync(intent.Intent.Identity, cancellationToken)
                ?? throw new InvalidOperationException("Completion archive intent disappeared.");
            if (intent.State == EffectLifecycle.Succeeded) break;
        }
        if (intent.State != EffectLifecycle.Succeeded || intent.Receipt is not { PostconditionSatisfied: true })
            throw new InvalidOperationException(
                $"Completion archive did not produce a verified receipt; current state is {intent.State}.");
        if (executor.Result is not null) return executor.Result;
        string? synthesis = await artifacts.ReadAsync(synthesisPath);
        return string.IsNullOrWhiteSpace(synthesis)
            ? throw new InvalidOperationException("Verified completion archive synthesis cannot be reconstructed.")
            : new CompletedEpicArchiveResult(index, archiveDirectory, synthesisPath, synthesis);
    }
}

internal sealed class CompletionArchiveEffectExecutor(
    ICompletedEpicArchiveService _inner,
    CompletedEpicArchiveRequest _request) : IEffectExecutor
{
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.CompletionArchive;
    public string Version => "1";
    public CompletedEpicArchiveResult? Result { get; private set; }

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        CompletionArchiveEffectPayload payload = Parse(intent);
        Result = await _inner.ArchiveAndSynthesizeAsync(_request, cancellationToken);
        bool satisfied = Result.Index == payload.Index &&
            Result.ArchiveDirectory == payload.ArchiveDirectory &&
            Result.SynthesisPath == payload.SynthesisPath &&
            !string.IsNullOrWhiteSpace(Result.SynthesisContent);
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Completed epic archive and synthesis were materialized."
                : "Completed epic archive result did not match its durable intent.",
            [Result.ArchiveDirectory, Result.SynthesisPath], "targets-absent",
            satisfied ? Result.SynthesisPath : "result-mismatch", satisfied, Result.SynthesisPath);
    }

    internal static CompletionArchiveEffectPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<CompletionArchiveEffectPayload>(
            intent.TypedPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("Completion archive effect payload is invalid.");
}

internal sealed class DeferredCompletionArchiveEffectExecutor : IEffectExecutor
{
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.CompletionArchive;
    public string Version => "1";
    public Task<EffectExecutionObservation> ExecuteAsync(EffectIntent intent, CancellationToken cancellationToken) =>
        Task.FromResult(new EffectExecutionObservation(
            EffectLifecycle.Pending,
            "Completion archive requires its authorized prompt/runtime context and remains pending.",
            [intent.Identity.Value], "not-dispatched", "pending-authorized-context", false));
}

internal sealed class CompletionArchiveEffectReconciler(
    Repository _repository,
    IArtifactStore _store) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        CompletionArchiveEffectPayload payload = CompletionArchiveEffectExecutor.Parse(intent);
        var artifacts = new CompletionArtifacts(_store, _repository);
        string? epic = await artifacts.ReadAsync($"{payload.ArchiveDirectory}/epic.md");
        string? synthesis = await artifacts.ReadAsync(payload.SynthesisPath);
        bool archiveStarted = await artifacts.ExistsAsync(payload.ArchiveDirectory) || epic is not null;
        if (!string.IsNullOrWhiteSpace(epic) && !string.IsNullOrWhiteSpace(synthesis))
        {
            return new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Archive structure and synthesis independently satisfy the completion postcondition.",
                [payload.ArchiveDirectory, payload.SynthesisPath], "unknown", payload.SynthesisPath,
                payload.SynthesisPath);
        }
        return new EffectReconciliationObservation(
            archiveStarted ? EffectReconciliationVerdict.HumanActionRequired : EffectReconciliationVerdict.NotApplied,
            archiveStarted
                ? "Completion archive is partially materialized and cannot be repeated automatically."
                : "Completion archive has not been applied.",
            [payload.ArchiveDirectory, payload.SynthesisPath], "unknown",
            archiveStarted ? "partial-archive" : "not-applied");
    }
}
