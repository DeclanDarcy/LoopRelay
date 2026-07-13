using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Effects;

internal sealed class DurableSurfaceRestoreEffectPlanner(Repository _repository)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public async Task RestoreAsync(
        CanonicalCausalContext causality,
        ArtifactMutationTransaction transaction,
        string operationIdentity,
        CancellationToken cancellationToken)
    {
        SurfaceRestoreEffectPayload payload = await transaction.CreateRestorePayloadAsync();
        string document = JsonSerializer.Serialize(payload, Options);
        var candidate = new EffectIntent(EffectIntentIdentity.New(), causality,
            $"surface-restore:{operationIdentity}", WorkspaceEffectExecutorKeys.SurfaceRestore, "1",
            new EffectTargetDescriptor("ScopedArtifactSurface", operationIdentity,
                JsonSerializer.Serialize(new { payload.ManifestHash }, Options)),
            document, payload.ManifestHash, 0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("captured-manifest", JsonSerializer.Serialize(new { payload.ManifestHash }, Options)),
            new EffectCondition("surface-equals-manifest", JsonSerializer.Serialize(new { payload.ManifestHash }, Options)),
            "surface-manifest-observation",
            $"surface-restore:{causality.TransitionRun.Value}:{operationIdentity}:{payload.ManifestHash}",
            DateTimeOffset.UtcNow);
        var store = new CanonicalEffectWorkStore(_repository);
        await store.AppendPlanAsync([candidate], cancellationToken);
        EffectWorkItem work = (await store.ReadBySemanticOperationAsync(candidate.SemanticOperationKey, cancellationToken))
            .Single(item => item.Intent.IdempotencyKey == candidate.IdempotencyKey);
        var worker = new EffectWorker($"surface-restore-{Environment.ProcessId}", store,
            new EffectExecutorRegistry([new SurfaceRestoreEffectExecutor(_repository)]),
            new SurfaceRestoreEffectReconciler(_repository), TimeSpan.FromMinutes(2));
        await worker.RunOnceAsync(cancellationToken, includePending: true,
            only: new HashSet<EffectIntentIdentity> { work.Intent.Identity });
        work = await store.ReadAsync(work.Intent.Identity, cancellationToken)
            ?? throw new InvalidOperationException("Surface-restore intent disappeared.");
        if (work.State != EffectLifecycle.Succeeded || work.Receipt is not { PostconditionSatisfied: true })
            throw new InvalidOperationException(
                $"Scoped artifact surface restore did not settle; current state is {work.State}.");
    }
}
