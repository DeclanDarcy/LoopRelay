using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Storage;

namespace LoopRelay.Cli.Services.Storage;

internal sealed class WorkspaceStorageApplicationService(
    Repository _repository,
    LoopRelay.Orchestration.Effects.EffectWorker _effectWorker,
    ICanonicalRecoveryCaseRecorder _recoveryCases)
{
    private readonly WorkspaceStorageInspector inspector = new();

    public Task<StorageInspection> VerifyAsync(CancellationToken cancellationToken) =>
        inspector.VerifyAsync(new StorageVerifyRequest(_repository.Path), cancellationToken);

    public async Task<StorageOperationResult> InitializeAsync(CancellationToken cancellationToken)
    {
        StorageInspection before = await VerifyAsync(cancellationToken);
        string target = LoopRelayWorkspaceDatabase.Resolve(_repository);
        string[] staged = Directory.Exists(Path.GetDirectoryName(target)!)
            ? Directory.GetFiles(Path.GetDirectoryName(target)!, $"{Path.GetFileName(target)}.*.storage-stage")
            : [];
        if (staged.Length == 1 && File.Exists(staged[0] + ".plan.json"))
        {
            StorageOperationPlan recoveredPlan = JsonSerializer.Deserialize<StorageOperationPlan>(
                await File.ReadAllTextAsync(staged[0] + ".plan.json", cancellationToken))
                ?? throw new InvalidDataException("Storage initialization staging plan is invalid.");
            return await CompleteStagedInitializationAsync(recoveredPlan, staged[0], target, cancellationToken);
        }
        if (before.Exists || before.PersistenceTree.Count > 0 || staged.Length > 0)
            return Refused(before, "Storage init requires an absent authority or exactly one valid recoverable staging plan.");
        CanonicalCausalContext causality = Causality(WorkspaceIdentity.New());
        StorageOperationIdentity operation = StorageOperationIdentity.New();
        string staging = $"{target}.{operation.Value}.storage-stage";
        string planPath = $"{staging}.plan.json";
        var plan = Plan(operation, StorageOperationKind.Initialize, causality, "absent",
            WorkspaceSchemaMigrationCatalog.Current.ShapeFingerprint,
            ["target-absent"], ["canonical-v14-valid", "workspace-identity-matches"]);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan), cancellationToken);
        try
        {
            _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(staging, cancellationToken);
            return await CompleteStagedInitializationAsync(plan, staging, target, cancellationToken);
        }
        catch
        {
            // Staging and plan remain restart-discoverable; an existing target is never overwritten.
            throw;
        }
    }

    private async Task<StorageOperationResult> CompleteStagedInitializationAsync(
        StorageOperationPlan plan,
        string staging,
        string target,
        CancellationToken cancellationToken)
    {
        WorkspaceSchemaInspection staged = await new WorkspaceSchemaReadOnlyInspector()
            .InspectAsync(staging, cancellationToken);
        if (staged.Shape != WorkspaceSchemaShape.CanonicalV15Complete)
            throw new InvalidDataException("Staged workspace did not validate as canonical v14.");
        string root = Path.GetFullPath(_repository.Path);
        string sourceRelative = Path.GetRelativePath(root, staging).Replace('\\', '/');
        string targetRelative = Path.GetRelativePath(root, target).Replace('\\', '/');
        string stagedHash;
        await using (FileStream stream = new(staging, FileMode.Open, FileAccess.Read, FileShare.Read))
            stagedHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        var payload = new StorageAuthorityPromotionEffectPayload(sourceRelative, targetRelative, stagedHash);
        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var intent = new EffectIntent(EffectIntentIdentity.New(), plan.Causality,
            $"storage:init-promote:{plan.Identity.Value}", WorkspaceEffectExecutorKeys.StorageAuthorityPromotion, "1",
            new EffectTargetDescriptor("CanonicalWorkspaceDatabase", targetRelative,
                JsonSerializer.Serialize(new { targetRelative })), payloadJson, payloadHash, 0, [],
            EffectRequiredness.BlockingLocal, new EffectCondition("target-absent", "{}"),
            new EffectCondition("target-sha256", JsonSerializer.Serialize(new { stagedHash })),
            "independent-target-hash", $"storage-init-promotion:{plan.Identity.Value}:{stagedHash}", DateTimeOffset.UtcNow);
        EffectExecutionObservation promotion = await new StorageAuthorityPromotionEffectExecutor(_repository)
            .ExecuteAsync(intent, cancellationToken);
        if (promotion.State != EffectLifecycle.Succeeded || !promotion.PostconditionSatisfied)
            throw new InvalidOperationException($"Storage initialization promotion failed: {promotion.Explanation}");
        File.Delete(staging);
        File.Delete(staging + ".plan.json");
        var store = new CanonicalStorageOperationStore(_repository);
        await store.PersistPlanAsync(plan, cancellationToken);
        StorageInspection after = await VerifyAsync(cancellationToken);
        EffectReceiptIdentity effectReceipt = EffectReceiptIdentity.New();
        await store.PersistReceiptAsync(new StorageOperationReceipt(plan.Identity,
            after.Schema?.ShapeFingerprint ?? "unknown", [effectReceipt],
            after.Evidence.Concat(promotion.Evidence).ToArray(), DateTimeOffset.UtcNow), cancellationToken);
        StorageInspection completed = await VerifyAsync(cancellationToken);
        return new StorageOperationResult(plan.Identity, StorageOperationLifecycle.Completed, completed, [intent.Identity],
            "Fresh canonical workspace authority initialized by validated absence-guarded promotion.", completed.Evidence);
    }

    public async Task<StorageOperationResult> MigrateAsync(CancellationToken cancellationToken)
    {
        StorageInspection before = await VerifyAsync(cancellationToken);
        if (!before.Exists || before.Health != StorageHealth.ActionRequired ||
            before.Schema?.Family != WorkspaceSchemaFamily.CanonicalWorkspace)
            return Refused(before, "Storage migrate requires one recognized canonical source that reports ActionRequired.");
        string target = LoopRelayWorkspaceDatabase.Resolve(_repository);
        string[] staged = Directory.GetFiles(Path.GetDirectoryName(target)!, $"{Path.GetFileName(target)}.*.storage-stage");
        StorageOperationPlan plan;
        string sidecar;
        if (staged.Length == 1)
        {
            sidecar = staged[0];
            plan = JsonSerializer.Deserialize<StorageOperationPlan>(
                await File.ReadAllTextAsync(sidecar, cancellationToken))
                ?? throw new InvalidDataException("Storage migration staging plan is invalid.");
            if (plan.Kind != StorageOperationKind.Migrate)
                return Refused(before, "A different interrupted storage operation must recover first.");
        }
        else
        {
            if (staged.Length > 1) return Refused(before, "Multiple migration staging plans are ambiguous.");
            StorageOperationIdentity operation = StorageOperationIdentity.New();
            CanonicalCausalContext causality = Causality(new WorkspaceIdentity(
                await ReadWorkspaceIdentityOrNewAsync(cancellationToken)));
            plan = Plan(operation, StorageOperationKind.Migrate, causality,
                before.Schema.ShapeFingerprint ?? before.ByteSha256 ?? "unknown",
                WorkspaceSchemaMigrationCatalog.Current.ShapeFingerprint,
                [$"source-shape:{before.Schema.Shape}"], [$"target-version:{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}"]);
            sidecar = target + $".{operation.Value}.storage-stage";
            await File.WriteAllTextAsync(sidecar, JsonSerializer.Serialize(plan), cancellationToken);
        }
        _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(
            target, cancellationToken);
        File.Delete(sidecar);
        var store = new CanonicalStorageOperationStore(_repository);
        await store.PersistPlanAsync(plan, cancellationToken);
        StorageInspection after = await VerifyAsync(cancellationToken);
        await store.PersistReceiptAsync(new StorageOperationReceipt(plan.Identity,
            after.Schema?.ShapeFingerprint ?? "unknown", [], after.Evidence, DateTimeOffset.UtcNow), cancellationToken);
        StorageInspection completed = await VerifyAsync(cancellationToken);
        return new StorageOperationResult(plan.Identity, StorageOperationLifecycle.Completed, completed, [],
            "Supported schema migration completed and verified.", completed.Evidence);
    }

    public async Task<StorageOperationResult> ExportAsync(string targetRelativePath, CancellationToken cancellationToken)
    {
        StorageInspection inspection = await VerifyAsync(cancellationToken);
        if (inspection.Health != StorageHealth.Healthy)
            return Refused(inspection, "Storage export requires a healthy current canonical authority.");
        var codec = new CanonicalStorageExportCodec();
        CanonicalStorageExportPackage package = await codec.ExportAsync(
            LoopRelayWorkspaceDatabase.Resolve(_repository), cancellationToken);
        string json = codec.Encode(package);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        CanonicalCausalContext causality = Causality(new WorkspaceIdentity(package.Manifest.WorkspaceIdentity));
        StorageOperationIdentity operation = StorageOperationIdentity.New();
        var plan = Plan(operation, StorageOperationKind.Export, causality,
            package.Manifest.LogicalFingerprint, "canonical-export-codec-v1",
            ["healthy-current-authority"], [$"package-sha256:{hash}"]);
        var store = new CanonicalStorageOperationStore(_repository);
        await store.PersistPlanAsync(plan, cancellationToken);
        var payload = new ExportPackageEffectPayload(targetRelativePath, Convert.ToBase64String(bytes), hash);
        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var intent = new EffectIntent(EffectIntentIdentity.New(), causality,
            $"storage:export:{operation.Value}", WorkspaceEffectExecutorKeys.ExportPackageWrite, "1",
            new EffectTargetDescriptor("CanonicalStorageExport", targetRelativePath,
                JsonSerializer.Serialize(new { targetRelativePath, package.Manifest.LogicalFingerprint })),
            payloadJson, hash, 0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("healthy-current-authority", "{}"),
            new EffectCondition("package-sha256", JsonSerializer.Serialize(new { hash })),
            "independent-package-hash", $"storage-export:{package.Manifest.LogicalFingerprint}:{targetRelativePath}",
            DateTimeOffset.UtcNow);
        var effects = new CanonicalEffectWorkStore(_repository);
        await effects.AppendPlanAsync([intent], cancellationToken);
        await store.AppendEventAsync(new StorageOperationEvent(operation, StorageOperationLifecycle.Effecting,
            "Canonical export package effect was planned.", [intent.Identity.Value], DateTimeOffset.UtcNow), cancellationToken);
        await _effectWorker.RunOnceAsync(cancellationToken, only: new HashSet<EffectIntentIdentity> { intent.Identity });
        EffectWorkItem completed = await effects.ReadAsync(intent.Identity, cancellationToken)
            ?? throw new InvalidOperationException("Storage export effect disappeared.");
        if (completed.Receipt is not { PostconditionSatisfied: true })
        {
            await RecordRecoveryAsync(operation, causality, completed.State.ToString(), cancellationToken);
            return new StorageOperationResult(operation, StorageOperationLifecycle.RecoveryRequired, inspection,
                [intent.Identity], "Storage export requires effect recovery.", [completed.State.ToString()]);
        }
        await store.PersistReceiptAsync(new StorageOperationReceipt(operation,
            package.Manifest.LogicalFingerprint, [completed.Receipt.Identity], completed.Receipt.Evidence,
            DateTimeOffset.UtcNow), cancellationToken);
        return new StorageOperationResult(operation, StorageOperationLifecycle.Completed, inspection,
            [intent.Identity], "Versioned semantic export package completed with a verified receipt.",
            [package.Manifest.LogicalFingerprint, package.Manifest.PackageSha256]);
    }

    public async Task<StorageOperationResult> SyncAsync(CancellationToken cancellationToken)
    {
        StorageInspection inspection = await VerifyAsync(cancellationToken);
        if (inspection.Health != StorageHealth.Healthy)
            return Refused(inspection, "Storage sync requires a healthy canonical authority.");
        CanonicalCausalContext causality = Causality(new WorkspaceIdentity(
            await ReadWorkspaceIdentityOrNewAsync(cancellationToken)));
        StorageOperationIdentity operation = StorageOperationIdentity.New();
        var plan = Plan(operation, StorageOperationKind.Sync, causality,
            inspection.Schema!.ShapeFingerprint!, "rebuildable-projections-and-journaled-effects",
            ["no-legacy-import", "no-authoritative-history-rewrite"], ["journaled-effects-reconciled"]);
        var store = new CanonicalStorageOperationStore(_repository);
        await store.PersistPlanAsync(plan, cancellationToken);
        EffectWorkerResult result = await _effectWorker.RunOnceAsync(cancellationToken);
        StorageOperationLifecycle lifecycle = result.RecoveryRequired > 0
            ? StorageOperationLifecycle.RecoveryRequired
            : StorageOperationLifecycle.Completed;
        await store.AppendEventAsync(new StorageOperationEvent(operation, lifecycle,
            lifecycle == StorageOperationLifecycle.Completed
                ? "Bounded sync reconciled only already-journaled effect work."
                : "Bounded sync discovered effect work requiring recovery.",
            result.Unsettled.Select(item => item.Value).ToArray(), DateTimeOffset.UtcNow), cancellationToken);
        if (lifecycle == StorageOperationLifecycle.RecoveryRequired)
            await RecordRecoveryAsync(operation, causality, "storage-sync-unsettled", cancellationToken);
        if (lifecycle == StorageOperationLifecycle.Completed)
            await store.PersistReceiptAsync(new StorageOperationReceipt(operation,
                inspection.Schema.ShapeFingerprint!, [], ["bounded-sync"], DateTimeOffset.UtcNow), cancellationToken);
        return new StorageOperationResult(operation, lifecycle, inspection, result.Unsettled,
            lifecycle == StorageOperationLifecycle.Completed ? "Storage sync completed." : "Storage sync requires recovery.",
            result.Unsettled.Select(item => item.Value).ToArray());
    }

    private async Task<string> ReadWorkspaceIdentityOrNewAsync(CancellationToken cancellationToken)
    {
        StorageInspection inspection = await VerifyAsync(cancellationToken);
        if (inspection.Schema?.Family != WorkspaceSchemaFamily.CanonicalWorkspace) return WorkspaceIdentity.New().Value;
        return await new WorkspaceSchemaReadOnlyInspector().ReadWorkspaceIdentityAsync(
            LoopRelayWorkspaceDatabase.Resolve(_repository), cancellationToken)
            ?? WorkspaceIdentity.New().Value;
    }

    private static StorageOperationPlan Plan(StorageOperationIdentity identity, StorageOperationKind kind,
        CanonicalCausalContext causality, string source, string target,
        IReadOnlyList<string> preconditions, IReadOnlyList<string> postconditions) => new(
        identity, kind, causality, source, target, preconditions, postconditions,
        $"storage:{kind}:{source}:{target}", DateTimeOffset.UtcNow);
    private static CanonicalCausalContext Causality(WorkspaceIdentity workspace) => new(
        workspace, RunIdentity.New(), WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New());
    private static StorageOperationResult Refused(StorageInspection inspection, string explanation) => new(
        null, StorageOperationLifecycle.Refused, inspection, [], explanation, inspection.Evidence);

    private async Task RecordRecoveryAsync(StorageOperationIdentity operation, CanonicalCausalContext causality,
        string evidence, CancellationToken cancellationToken)
    {
        var subject = new RecoveryCausalSubject(causality, StorageOperationIdentity: operation.Value);
        _ = await _recoveryCases.RecordAsync(RecoveryScopeKind.StorageOperation, subject,
            new RecoveryDurableFacts(RecoveryScopeKind.StorageOperation, subject, true, false, true, false,
                true, true, false, false, false, false, false, false, RecoveryCancellationBoundary.None,
                1, 0, false, false, [evidence]), cancellationToken);
    }
}
