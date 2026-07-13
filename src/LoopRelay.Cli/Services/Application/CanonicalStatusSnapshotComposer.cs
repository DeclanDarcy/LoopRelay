using System.Text.Json;
using LoopRelay.Application.ReadModel;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;

namespace LoopRelay.Cli.Services.Application;

internal static class CanonicalStatusSnapshotComposer
{
    public static async Task<CanonicalCliStatusSnapshot> ProjectStatusAsync(
        LoopRelayCompositionRoot composition,
        RepositoryObservation observation,
        WorkflowResolutionResult resolution,
        CancellationToken cancellationToken)
    {
        DecisionContinuityStatusSnapshot? continuity = null;
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(composition.Repository);
        bool healthyStorage = observation.StorageVerification.Health == StorageHealth.Healthy;
        if (File.Exists(databasePath) && healthyStorage)
        {
            continuity = await new CanonicalDecisionRecoveryStore(
                composition.Repository,
                new SqliteRecoveryStore(composition.Repository)).ReadStatusAsync(cancellationToken);
        }
        IReadOnlyList<ConsumedInputDrift> inputDrift = observation.StorageAuthority.UsableAuthority
            ? await ReadReceiptStaleness.ProjectAsync(composition.Repository, cancellationToken)
            : [];
        CanonicalWorkflowPersistenceSnapshot workflow = healthyStorage
            ? await composition.Persistence.LoadSnapshotAsync(cancellationToken)
            : new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], []);
        IReadOnlyList<string> pendingEffects = workflow.EffectRecords
            .GroupBy(effect => (effect.RunId, effect.Effect))
            .Select(group => group.OrderByDescending(effect => effect.RecordId).First())
            .Where(effect => effect.Status is not EffectExecutionStatus.Succeeded)
            .Select(effect => $"{effect.Effect}:{effect.Status}")
            .ToArray();
        IReadOnlyList<string> pendingDispatches = healthyStorage
            ? (await composition.Persistence.ReadPromptDispatchEventsAsync(cancellationToken))
                .GroupBy(item => item.DispatchId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .Where(item => item.State is PromptDispatchState.Planned or PromptDispatchState.Authorized
                    or PromptDispatchState.Started or PromptDispatchState.Unknown)
                .Select(item => $"{item.DispatchId}:{item.State}")
                .ToArray()
            : [];
        IReadOnlyList<InteractionAggregate> interactions = File.Exists(databasePath) && healthyStorage
            ? await composition.InteractionBroker.ListAsync(new ListInteractionsQuery(), cancellationToken)
            : [];
        IReadOnlyList<string> requiredActions = resolution.Explanation.Warnings
            .Select(warning => warning.Remediation)
            .Concat(continuity?.Diagnostic is null ? [] : [continuity.Diagnostic])
            .Concat(interactions.Select(interaction =>
                $"Interaction {interaction.Request.Identity.Value} [{interaction.Request.Category}/{interaction.State}]: {interaction.Request.Question}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CanonicalWorkspaceSnapshot workspaceSnapshot = await ComposeAsync(
            composition, observation, resolution, continuity, inputDrift, pendingEffects,
            pendingDispatches, interactions, cancellationToken);
        return new CanonicalCliStatusSnapshot(
            composition.Repository.Path,
            observation,
            resolution,
            continuity,
            inputDrift,
            pendingEffects,
            pendingDispatches,
            interactions.Select(interaction =>
                $"{interaction.Request.Policy.Identity.Value}:{interaction.Request.Policy.ResolvedPolicyIdentity}").ToArray(),
            observation.StorageVerification.UnsupportedSchema
                .Concat(observation.StorageVerification.Conflicts)
                .ToArray(),
            requiredActions,
            workspaceSnapshot);
    }

    public static async Task<CanonicalWorkspaceSnapshot> ComposeAsync(
        LoopRelayCompositionRoot composition,
        RepositoryObservation observation,
        WorkflowResolutionResult resolution,
        DecisionContinuityStatusSnapshot? continuity,
        IReadOnlyList<ConsumedInputDrift> inputDrift,
        IReadOnlyList<string> pendingEffects,
        IReadOnlyList<string> pendingDispatches,
        IReadOnlyList<InteractionAggregate> interactions,
        CancellationToken cancellationToken)
    {
        CompletionAuthorityProjectionSnapshot? completion = null;
        if (observation.StorageAuthority.UsableAuthority)
            completion = await new CompletionAuthorityProjection(
                new CanonicalCompletionAuthorityStore(composition.Repository)).ProjectAsync(cancellationToken);
        string workspace = observation.StorageAuthority.UsableAuthority
            ? await composition.Persistence.ReadWorkspaceIdentityAsync(cancellationToken)
            : $"unknown:{Path.GetFullPath(composition.Repository.Path)}";
        List<ICanonicalOwnerProjection> projections =
        [
            Projection(CanonicalProjectionOwners.Storage, "storage:1", new
            {
                observation.StorageVerification.Authority,
                observation.StorageVerification.Health,
                observation.StorageVerification.Schema,
                observation.StorageVerification.RequiredActions,
            }, observation.StorageVerification.Evidence),
            Projection(CanonicalProjectionOwners.Workflow, "workflow:1", new
            {
                resolution.Classification,
                resolution.Selection,
                resolution.SelectedStage,
                resolution.TransitionEligibility,
                observation.WorkflowStates,
                observation.TransitionRuns,
            }, resolution.Explanation.Evidence),
            Projection(CanonicalProjectionOwners.Products, "products:1", new
            {
                observation.Products,
                inputDrift,
            }, observation.Products.SelectMany(item => item.Evidence).ToArray()),
            Projection(CanonicalProjectionOwners.PolicyRuntime, "policy-runtime:1", new
            {
                policy = composition.Policy.PolicyId,
                composition.RuntimeProfile,
                composition.PromptPolicyProfile,
                rolePolicy = composition.AgentRolePolicy.Identity,
            }, [composition.Policy.PolicyId, composition.RuntimeProfile.Value]),
            Projection(CanonicalProjectionOwners.Dispatch, "dispatch:1", new { pendingDispatches },
                pendingDispatches.Count == 0 ? ["dispatch:none-pending"] : pendingDispatches),
            Projection(CanonicalProjectionOwners.Effects, "effects:1", new { pendingEffects },
                pendingEffects.Count == 0 ? ["effects:none-pending"] : pendingEffects),
            Projection(CanonicalProjectionOwners.Recovery, "recovery:1", new { continuity },
                continuity is null ? ["recovery:none-active"] :
                    [continuity.Active?.ScopeId ?? continuity.Diagnostic ?? "recovery:known-empty"]),
            Projection(CanonicalProjectionOwners.Interaction, "interaction:1", interactions.Select(item => new
            {
                identity = item.Request.Identity.Value,
                item.Request.Category,
                item.State,
                responseSchema = item.Request.Policy.ResponseJsonSchema,
                item.Request.Policy.Deadline,
                policy = item.Request.Policy.Identity.Value,
            }).ToArray(), interactions.Count == 0 ? ["interaction:none-open"] :
                interactions.Select(item => item.Request.Identity.Value).ToArray()),
            Projection(CanonicalProjectionOwners.Completion, completion?.Watermark ?? "completion:empty", new
            {
                completion?.LatestDecision,
                completion?.Certificate,
                completion?.ClosurePlan,
                completion?.LatestSettlement,
                completion?.TerminalFact,
                completion?.PendingOperations,
            }, completion is null ? ["completion:unknown"] :
                [completion.LatestDecision?.Identity.Value ?? "completion:no-decision"]),
            Projection(CanonicalProjectionOwners.Certification,
                $"certification:{composition.WorkflowCatalog.Identity}:{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}",
                composition.WorkflowCatalog.Obligations
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new
                    {
                        obligationKey = item.Key,
                        contentVersion = item.ContentHash,
                        catalogIdentity = composition.WorkflowCatalog.Identity,
                        schemaIdentity = $"{LoopRelayWorkspaceDatabase.SchemaIdentity}:v{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}",
                        assetVersion = composition.PromptPolicyProfile.Value,
                        providerProfile = composition.RuntimeProfile.Value,
                        evidenceTier = "Uncovered",
                        evidenceIdentity = (string?)null,
                        creditStatus = "Uncredited",
                        reason = "No exact workspace certification evidence link credits this obligation.",
                    }).ToArray(),
                composition.WorkflowCatalog.Obligations.Select(item => item.Key).ToArray()),
            Projection(CanonicalProjectionOwners.Release,
                $"release:local-only:{composition.RuntimeProfile.Value}",
                new
                {
                    provenance = "LocalOnly",
                    crossMachineCredited = false,
                    exactProviderProfile = composition.RuntimeProfile.Value,
                    promotionRequires = new[] { "static-protocol-fixtures", "live-capability-evidence" },
                    retirementRequires = new[]
                    {
                        "zero-active-root-references",
                        "zero-attempt-references",
                        "zero-session-references",
                        "zero-recovery-plan-references",
                        "zero-evidence-claim-references",
                        "proven-replacement-profile",
                    },
                    reason = "Local status has no D9 durable cross-machine release evidence owner.",
                },
                [$"profile:{composition.RuntimeProfile.Value}", "release:local-only"]),
        ];
        return await new CanonicalWorkspaceSnapshotComposer(projections).ComposeAsync(
            workspace,
            $"{LoopRelayWorkspaceDatabase.SchemaIdentity}:v{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}",
            composition.WorkflowCatalog.Identity,
            cancellationToken);
    }

    private static ICanonicalOwnerProjection Projection<T>(
        string owner, string watermark, T value, IReadOnlyList<string> evidence) =>
        new FixedProjection(new OwnerProjectionSection(owner, watermark,
        [
            new CanonicalClaim(owner, JsonSerializer.Serialize(value), ClaimKnowledge.Known, owner,
                evidence.Count == 0 ? [$"{owner}:known-empty"] : evidence.Distinct(StringComparer.Ordinal).ToArray(),
                watermark, "v1"),
        ]));

    private sealed class FixedProjection(OwnerProjectionSection section) : ICanonicalOwnerProjection
    {
        public string Owner => section.Owner;
        public Task<OwnerProjectionResult> ProjectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new OwnerProjectionResult(section, section.Watermark, section.Watermark));
    }
}
