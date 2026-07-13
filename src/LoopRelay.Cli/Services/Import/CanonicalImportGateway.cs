using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Import;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Storage;

namespace LoopRelay.Cli.Services.Import;

internal sealed class CanonicalImportGateway(Repository _repository) : IImportGateway
{
    private readonly ImportPortfolioDetector detector = new();

    internal async Task<ImportPreview> ReadPreviewForApprovalAsync(
        ImportPreviewIdentity identity, CancellationToken token)
    {
        (_, CanonicalImportStore store) = await LocateStageAsync(identity.Value, token);
        return await store.ReadPreviewAsync(identity, token)
            ?? throw new KeyNotFoundException($"Import preview `{identity}` was not found.");
    }

    public async Task<ImportResult> DetectAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ImportDetection detection = await detector.DetectAsync(repositoryPath, cancellationToken);
        (Repository stage, CanonicalImportStore store) = await CreateStageAsync(detection, cancellationToken);
        var evidence = detection.Evidence.ToList();
        if (detection.SourceKind == ImportSourceKind.Ambiguous)
        {
            var broker = new InteractionBroker(new CanonicalInteractionStore(stage));
            CanonicalCausalContext causality = new(WorkspaceIdentity.New(), RunIdentity.New(),
                WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New());
            InteractionCategoryPolicy policy = InteractionCategoryPolicyRegistry.Resolve(
                InteractionCategory.ImportConflict, "import-gateway-detection-policy");
            var request = new InteractionRequest(InteractionRequestIdentity.New(), InteractionCategory.ImportConflict,
                new InteractionCausalSubject(causality, "import-detection", detection.Identity.Value),
                "Select or repair the conflicting import source before preview.",
                JsonSerializer.Serialize(new { detection.Conflicts, detection.Evidence }), policy,
                detection.Conflicts, $"import-conflict:{detection.SourceFingerprint}", DateTimeOffset.UtcNow);
            InteractionAggregate aggregate = await broker.CreateAsync(new(request), cancellationToken);
            aggregate = await broker.PresentAsync(aggregate.Request.Identity, aggregate.RowVersion, cancellationToken);
            evidence.Add($"interaction:{aggregate.Request.Identity.Value}");
        }
        return new ImportResult(
            detection.CanPreview ? ImportLifecycle.Detected :
                detection.SourceKind == ImportSourceKind.Ambiguous ? ImportLifecycle.ApprovalRequired : ImportLifecycle.Refused,
            detection, null, null, null,
            detection.CanPreview ? "Owned import source detected without modifying source bytes."
                : "Import detection failed closed; resolve the reported source problem.", evidence);
    }

    public async Task<ImportResult> PreviewAsync(
        ImportDetectionIdentity detectionIdentity,
        CancellationToken cancellationToken = default)
    {
        (Repository _, CanonicalImportStore store) = await LocateStageAsync(detectionIdentity.Value, cancellationToken);
        ImportDetection detection = await store.ReadDetectionAsync(detectionIdentity, cancellationToken)
            ?? throw new KeyNotFoundException($"Import detection `{detectionIdentity}` was not found.");
        ImportDetection current = await detector.DetectAsync(detection.RepositoryPath, cancellationToken);
        if (!string.Equals(current.SourceFingerprint, detection.SourceFingerprint, StringComparison.Ordinal))
            return new ImportResult(ImportLifecycle.Refused, current, null, null, null,
                "Source changed after detection; create a new preview.", [detection.SourceFingerprint, current.SourceFingerprint]);
        if (detection.SourceKind == ImportSourceKind.CanonicalMigrationRequired)
            return new ImportResult(ImportLifecycle.Refused, detection, null, null, null,
                "Canonical schema convergence belongs to `storage migrate`, not Import Gateway.", detection.Evidence);
        ImportPreview preview = await detector.PreviewAsync(detection, cancellationToken);
        await store.PersistPreviewAsync(preview, cancellationToken);
        return new ImportResult(ImportLifecycle.ApprovalRequired, detection, preview, null, null,
            "Durable import preview created; explicit approval is required.",
            [preview.Identity.Value, preview.Detection.SourceFingerprint]);
    }

    public async Task<ImportResult> ApproveAsync(ImportApproval approval, CancellationToken cancellationToken = default)
    {
        (Repository _, CanonicalImportStore store) = await LocateStageAsync(approval.Preview.Value, cancellationToken);
        ImportPreview preview = await store.ReadPreviewAsync(approval.Preview, cancellationToken)
            ?? throw new KeyNotFoundException($"Import preview `{approval.Preview}` was not found.");
        if (approval.AuthorizationEvidence.Count == 0 || string.IsNullOrWhiteSpace(approval.ApproverIdentity))
            return new ImportResult(ImportLifecycle.Refused, preview.Detection, preview, null, null,
                "Import approval requires authenticated authorization evidence.", []);
        ImportDetection current = await detector.DetectAsync(preview.Detection.RepositoryPath, cancellationToken);
        if (!string.Equals(current.SourceFingerprint, approval.SourceFingerprint, StringComparison.Ordinal) ||
            !string.Equals(current.SourceFingerprint, preview.Detection.SourceFingerprint, StringComparison.Ordinal))
            return new ImportResult(ImportLifecycle.Refused, current, preview, null, null,
                "Import preview is stale; source fingerprint changed before approval.",
                [preview.Detection.SourceFingerprint, current.SourceFingerprint]);
        ImportOperationIdentity operation = ImportOperationIdentity.New();
        string planHash = Hash(JsonSerializer.Serialize(new
        {
            preview = preview.Identity.Value,
            preview.Detection.SourceFingerprint,
            mappings = preview.Mappings,
        }));
        await store.ApproveAndPlanAsync(approval, operation, planHash, cancellationToken);
        return new ImportResult(ImportLifecycle.Approved, preview.Detection, preview, operation, null,
            "Import approval and immutable plan were persisted.", [operation.Value, planHash]);
    }

    public async Task<ImportResult> ExecuteAsync(ImportPreviewIdentity previewIdentity,
        CancellationToken cancellationToken = default)
    {
        (Repository stageRepository, CanonicalImportStore control) = await LocateStageAsync(previewIdentity.Value, cancellationToken);
        ImportPreview preview = await control.ReadPreviewAsync(previewIdentity, cancellationToken)
            ?? throw new KeyNotFoundException($"Import preview `{previewIdentity}` was not found.");
        ImportApproval approval = await control.ReadApprovalAsync(previewIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Import execution requires a persisted explicit approval.");
        ImportOperationIdentity operation = await control.ReadOperationAsync(previewIdentity, cancellationToken)
            ?? throw new InvalidOperationException("Approved import preview has no immutable operation plan.");
        string target = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (File.Exists(target))
        {
            try
            {
                ImportReceipt? existing = await new CanonicalImportStore(target)
                    .ReadReceiptBySourceFingerprintAsync(preview.Detection.SourceFingerprint, cancellationToken);
                if (existing is not null)
                    return new ImportResult(ImportLifecycle.Completed, preview.Detection, preview,
                        existing.Operation, existing, "Identical import already completed; existing receipt returned.", existing.Evidence);
            }
            catch (Exception exception) when (exception is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
            {
                // The target may still be the legacy source; normal detection and mapping continue below.
            }
        }
        ImportDetection current = await detector.DetectAsync(preview.Detection.RepositoryPath, cancellationToken);
        if (!string.Equals(current.SourceFingerprint, preview.Detection.SourceFingerprint, StringComparison.Ordinal))
            return new ImportResult(ImportLifecycle.Refused, current, preview, operation, null,
                "Import source changed after approval; no canonical writes were performed.",
                [preview.Detection.SourceFingerprint, current.SourceFingerprint]);

        string working = Path.Combine(Path.GetDirectoryName(target)!, $"import-{operation.Value}.sqlite3");
        CanonicalImportStore targetStore = control;
        string workingDatabase = LoopRelayWorkspaceDatabase.Resolve(stageRepository);
        if (preview.Detection.Adapters.Any(item => item.SourceKind == ImportSourceKind.LegacyContinuityV3))
        {
            WorkspaceCompatibilityImportResult imported = await LegacyContinuityWorkspaceImporter.ImportToShadowAsync(
                target, working, cancellationToken);
            operation = new ImportOperationIdentity(imported.ImportId);
            targetStore = new CanonicalImportStore(working);
            await targetStore.PersistDetectionAsync(preview.Detection, cancellationToken);
            await targetStore.PersistPreviewAsync(preview, cancellationToken);
            await targetStore.AdoptApprovedAsync(approval, operation, cancellationToken);
            workingDatabase = working;
        }
        else if (preview.Detection.Adapters.Any(item => item.SourceKind == ImportSourceKind.CanonicalExportPackage))
        {
            string packagePath = preview.Detection.Evidence.Single(item => item.EndsWith(".canonical.json", StringComparison.Ordinal));
            CanonicalStorageExportPackage package = new CanonicalStorageExportCodec().Decode(
                await File.ReadAllTextAsync(Path.Combine(_repository.Path, packagePath.Replace('/', Path.DirectorySeparatorChar)), cancellationToken));
            if (File.Exists(working)) File.Delete(working);
            await new CanonicalStorageExportCodec().RehydrateFreshAsync(package, working, cancellationToken);
            targetStore = new CanonicalImportStore(working);
            await targetStore.PersistDetectionAsync(preview.Detection, cancellationToken);
            await targetStore.PersistPreviewAsync(preview, cancellationToken);
            await targetStore.ApproveAndPlanAsync(approval, operation, Hash(preview.Identity.Value), cancellationToken);
            workingDatabase = working;
        }
        else
        {
            if (File.Exists(working)) File.Delete(working);
            _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(working, cancellationToken);
            targetStore = new CanonicalImportStore(working);
            await targetStore.PersistDetectionAsync(preview.Detection, cancellationToken);
            await targetStore.PersistPreviewAsync(preview, cancellationToken);
            await targetStore.ApproveAndPlanAsync(approval, operation, Hash(preview.Identity.Value), cancellationToken);
            await targetStore.StageFilesystemProductFactsAsync(_repository.Path, preview, cancellationToken);
            workingDatabase = working;
        }

        CanonicalStorageExportPackage targetProjection = await new CanonicalStorageExportCodec()
            .ExportAsync(workingDatabase, cancellationToken);
        string[] missingDomains = preview.Mappings.Select(item => item.Domain).Distinct(StringComparer.Ordinal)
            .Where(domain => !preview.SemanticDelta.Any(delta => delta.Domain == domain)).ToArray();
        var verification = new ImportVerification(missingDomains.Length == 0, missingDomains,
            targetProjection.Manifest.LogicalFingerprint, DateTimeOffset.UtcNow);
        if (!verification.Equivalent)
            return new ImportResult(ImportLifecycle.Refused, preview.Detection, preview, operation, null,
                "Semantic import verification failed.", missingDomains);
        ImportReceiptIdentity receiptIdentity = ImportReceiptIdentity.New();
        string root = Path.GetFullPath(_repository.Path);
        string sourceRelative = Path.GetRelativePath(root, workingDatabase).Replace('\\', '/');
        string targetRelative = Path.GetRelativePath(root, target).Replace('\\', '/');
        string? archiveRelative = File.Exists(target)
            ? Path.GetRelativePath(root, target + $".legacy-nonauthoritative-{receiptIdentity.Value}").Replace('\\', '/')
            : null;
        string expectedHash = await ImportAuthorityPromotionEffectExecutor.HashExistingAsync(
            workingDatabase, cancellationToken);
        var payload = new ImportAuthorityPromotionEffectPayload(
            sourceRelative, targetRelative, archiveRelative, expectedHash);
        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        string payloadHash = Hash(payloadJson);
        CanonicalCausalContext causality = new(WorkspaceIdentity.New(), RunIdentity.New(),
            WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New());
        var intent = new EffectIntent(EffectIntentIdentity.New(), causality,
            $"import:promote:{operation.Value}", WorkspaceEffectExecutorKeys.ImportAuthorityPromotion, "1",
            new EffectTargetDescriptor("CanonicalWorkspaceDatabase", targetRelative,
                JsonSerializer.Serialize(new { targetRelative, operation = operation.Value })),
            payloadJson, payloadHash, 0, [], EffectRequiredness.BlockingLocal,
            new EffectCondition("source-sha256", JsonSerializer.Serialize(new { expectedHash })),
            new EffectCondition("target-sha256", JsonSerializer.Serialize(new { expectedHash })),
            "independent-target-hash", $"import-promotion:{preview.Detection.SourceFingerprint}",
            DateTimeOffset.UtcNow);
        var effectStore = new CanonicalEffectWorkStore(stageRepository);
        await effectStore.AppendPlanAsync([intent], cancellationToken);
        // The import mapper and verifier may have opened both source and target through SQLite's
        // shared pool. Release those handles before the M8 executor performs the planned atomic
        // rename; otherwise Windows correctly reports an unknown outward outcome for a file that
        // this process itself still has pooled.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var worker = new EffectWorker($"import-{Environment.ProcessId}", effectStore,
            new EffectExecutorRegistry([new ImportAuthorityPromotionEffectExecutor(_repository)]),
            new ImportAuthorityPromotionEffectReconciler(_repository), TimeSpan.FromMinutes(2));
        _ = await worker.RunOnceAsync(cancellationToken, only: new HashSet<EffectIntentIdentity> { intent.Identity });
        EffectWorkItem settled = await effectStore.ReadAsync(intent.Identity, cancellationToken)
            ?? throw new InvalidOperationException("Import promotion effect disappeared.");
        if (settled.Receipt is not { PostconditionSatisfied: true } effectReceipt)
        {
            EffectLifecycleEvent? latest = settled.Events.LastOrDefault();
            return new ImportResult(ImportLifecycle.RecoveryRequired, preview.Detection, preview, operation, null,
                latest?.Explanation ?? "Import facts were verified, but authority promotion requires M8/M9 reconciliation.",
                [intent.Identity.Value, settled.State.ToString(), .. latest?.Evidence ?? []]);
        }

        targetStore = new CanonicalImportStore(target);
        var receipt = new ImportReceipt(receiptIdentity, operation, preview.Identity,
            preview.Detection.SourceFingerprint, verification.TargetLogicalFingerprint,
            preview.Mappings.Select(mapping => $"{mapping.SourceIdentity}->{mapping.TargetIdentity}")
                .Concat([effectReceipt.Identity.Value]).ToArray(), DateTimeOffset.UtcNow);
        await targetStore.CompleteAsync(operation, preview, verification, receipt, CancellationToken.None);
        return new ImportResult(ImportLifecycle.Completed, preview.Detection, preview, operation, receipt,
            "Import completed after semantic verification; canonical-only authority is monotonic.",
            receipt.Evidence.Concat([$"canonical-only:{receipt.Identity.Value}"]).ToArray());
    }

    public async Task<ImportResult> VerifyAsync(
        string importIdentity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(importIdentity))
            return new ImportResult(ImportLifecycle.Refused, null, null, null, null,
                "Import verification requires a receipt, operation, or preview identity.", []);
        string target = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(target))
            return new ImportResult(ImportLifecycle.Refused, null, null, null, null,
                "Canonical workspace authority does not exist.", []);
        ImportReceipt? receipt = await new CanonicalImportStore(target)
            .ReadReceiptAsync(importIdentity, cancellationToken);
        if (receipt is null)
            return new ImportResult(ImportLifecycle.Refused, null, null, null, null,
                $"Import identity `{importIdentity}` has no canonical receipt.", [importIdentity]);
        return new ImportResult(ImportLifecycle.Verified, null, null, receipt.Operation, receipt,
            "Canonical import receipt and its evidence chain are present.",
            receipt.Evidence.Concat([receipt.Identity.Value, receipt.TargetLogicalFingerprint])
                .Distinct(StringComparer.Ordinal).ToArray());
    }

    private async Task<(Repository Repository, CanonicalImportStore Store)> CreateStageAsync(
        ImportDetection detection, CancellationToken token)
    {
        string root = StageRoot(detection.Identity.Value);
        Directory.CreateDirectory(root);
        var repository = new Repository { Id = Guid.NewGuid(), Name = detection.Identity.Value, Path = root };
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        _ = await new WorkspaceSchemaMigrationExecutor().ExecuteAsync(database, token);
        var store = new CanonicalImportStore(database);
        await store.PersistDetectionAsync(detection, token);
        return (repository, store);
    }

    private async Task<(Repository Repository, CanonicalImportStore Store)> LocateStageAsync(
        string identity, CancellationToken token)
    {
        string parent = Path.Combine(_repository.Path, ".LoopRelay", "import-staging");
        foreach (string directory in Directory.Exists(parent) ? Directory.GetDirectories(parent) : [])
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(directory), Path = directory };
            string database = LoopRelayWorkspaceDatabase.Resolve(repository);
            if (!File.Exists(database)) continue;
            var store = new CanonicalImportStore(database);
            // Identity lookup is completed by the caller; returning every stage would be ambiguous,
            // so file-name detection IDs and contained preview IDs are searched synchronously below.
            if (Path.GetFileName(directory).Equals(identity, StringComparison.Ordinal) ||
                await store.ContainsPreviewAsync(identity, token))
                return (repository, store);
        }
        throw new KeyNotFoundException($"Import staging identity `{identity}` was not found.");
    }

    private string StageRoot(string detection) => Path.Combine(_repository.Path, ".LoopRelay", "import-staging", detection);
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
