using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Cli.Services.Effects;

internal sealed class DurableFilesystemWriteEffectPlanner(Repository _repository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ScheduleAsync(
        CanonicalCausalContext causality,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var store = new CanonicalEffectWorkStore(_repository);
        IReadOnlyList<EffectWorkItem> plan = await store.ReadPlanAsync(
            causality.TransitionRun, cancellationToken);
        EffectWorkItem parent = plan
            .Where(item => item.State == EffectLifecycle.Started &&
                item.Intent.Causality.Attempt == causality.Attempt &&
                item.Intent.Executor.Value.StartsWith("canonical-transition-effect:", StringComparison.Ordinal))
            .OrderByDescending(item => item.Intent.Order)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Filesystem writes may only be scheduled by a started canonical feature effect.");
        var payload = new FilesystemWriteEffectPayload(relativePath, content);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var intent = new EffectIntent(
            EffectIntentIdentity.New(),
            causality,
            $"filesystem:write:{relativePath}",
            WorkspaceEffectExecutorKeys.FilesystemWrite,
            "1",
            new EffectTargetDescriptor("Filesystem", relativePath,
                JsonSerializer.Serialize(new { relativePath }, JsonOptions)),
            payloadJson,
            payloadHash,
            parent.Intent.Order + 1,
            [parent.Intent.Identity],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("workspace-contained", "{}"),
            new EffectCondition("content-hash", JsonSerializer.Serialize(new { payloadHash }, JsonOptions)),
            "independent-content-hash",
            $"filesystem-write:{parent.Intent.Identity.Value}:{relativePath}:{payloadHash}",
            DateTimeOffset.UtcNow);
        await store.AppendPlanAsync([intent], cancellationToken);
    }

    public async Task WriteCandidateAsync(
        CanonicalCausalContext causality,
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var payload = new FilesystemWriteEffectPayload(relativePath, content);
        string payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        string idempotencyKey =
            $"candidate-filesystem-write:{causality.TransitionRun.Value}:{relativePath}:{payloadHash}";
        var candidate = new EffectIntent(
            EffectIntentIdentity.New(),
            causality,
            $"candidate-filesystem:write:{relativePath}",
            WorkspaceEffectExecutorKeys.FilesystemWrite,
            "1",
            new EffectTargetDescriptor(
                "Filesystem",
                relativePath,
                JsonSerializer.Serialize(new { relativePath }, JsonOptions)),
            payloadJson,
            payloadHash,
            0,
            [],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("workspace-contained", "{}"),
            new EffectCondition("content-hash", JsonSerializer.Serialize(new { payloadHash }, JsonOptions)),
            "independent-content-hash",
            idempotencyKey,
            DateTimeOffset.UtcNow);
        var store = new CanonicalEffectWorkStore(_repository);
        await store.AppendPlanAsync([candidate], cancellationToken);
        EffectWorkItem intent = (await store.ReadBySemanticOperationAsync(
                candidate.SemanticOperationKey,
                cancellationToken))
            .Single(item => item.Intent.IdempotencyKey == idempotencyKey);
        var executor = new FilesystemWriteEffectExecutor(_repository);
        var worker = new EffectWorker(
            $"candidate-filesystem-{Environment.ProcessId}",
            store,
            new EffectExecutorRegistry([executor]),
            new FilesystemWriteEffectReconciler(_repository),
            TimeSpan.FromMinutes(2));
        await worker.RunOnceAsync(
            cancellationToken,
            includePending: true,
            only: new HashSet<EffectIntentIdentity> { intent.Intent.Identity });
        intent = await store.ReadAsync(intent.Intent.Identity, cancellationToken)
            ?? throw new InvalidOperationException("Candidate filesystem-write intent disappeared.");
        if (intent.State != EffectLifecycle.Succeeded || intent.Receipt is not { PostconditionSatisfied: true })
        {
            throw new InvalidOperationException(
                $"Candidate filesystem write `{relativePath}` did not produce a verified receipt; " +
                $"current state is {intent.State}.");
        }
    }
}
