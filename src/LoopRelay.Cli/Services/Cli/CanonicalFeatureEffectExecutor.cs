using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Agents.Services.Codex.Compatibility;
using LoopRelay.Agents.Services.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Cli.Services.Decisions.Recovery;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Effects;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Infrastructure.Services.Effects;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Context;
using LoopRelay.Projections.Services.Definitions;
using LoopRelay.Projections.Services.Manifests;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using Microsoft.Extensions.DependencyInjection;
using static LoopRelay.Cli.Services.Cli.LoopRelayCompositionRoot;

namespace LoopRelay.Cli.Services.Cli;

internal sealed class CanonicalFeatureEffectExecutor(
    Repository _repository,
    CanonicalWorkflowPersistenceStore _store,
    IReadOnlyList<WorkflowDefinition> _definitions) : LoopRelay.Orchestration.Runtime.IEffectExecutor, ITransitionEffectIntentExecutor
{
    public async Task<EffectExecutionRecord> ExecuteAsync(
        CanonicalCausalContext causality,
        EffectIdentity effect,
        CancellationToken cancellationToken)
    {
        WorkflowTransitionDefinition definition = _definitions
            .SelectMany(workflow => workflow.Transitions)
            .Single(transition => transition.Effects.Any(candidate => candidate.Identity == effect));
        EffectDefinition requestedEffect = definition.Effects.Single(candidate => candidate.Identity == effect);
        HashSet<ProductIdentity> produced = definition.ProducedProducts
            .Select(product => product.Identity)
            .ToHashSet();
        CanonicalWorkflowPersistenceSnapshot snapshot = await _store.LoadSnapshotAsync(cancellationToken);
        ProductRecord[] products = snapshot.Products
            .Where(product => produced.Contains(product.Identity))
            .ToArray();
        var validation = new ProductValidationResult(
            ProductValidationStatus.Valid,
            products,
            [],
            [],
            [],
            [],
            "Canonical committed products were re-observed for effect execution.",
            [causality.TransitionRun.Value]);
        EffectExecutionResult result = await ExecuteAsync(
            definition,
            validation,
            new EffectExecutionContext(causality),
            cancellationToken);
        return result.Effects.FirstOrDefault(record => record.Effect == effect)
            ?? new EffectExecutionRecord(effect, result.Status, result.Explanation, result.Evidence);
    }

    public async Task<EffectExecutionResult> ExecuteAsync(
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        EffectExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (LocalArtifactTransitions.Supports(definition))
        {
            return await ExecuteLocalArtifactAsync(definition, validation, context, cancellationToken);
        }

        if (PlanWarmSessionTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? PlanWarmSessionTransitions.Evidence(definition)
                    : validation.Evidence,
                "Plan warm-session",
                context,
                cancellationToken);
        }

        if (PlanProjectionTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? PlanProjectionTransitions.Evidence(definition)
                    : validation.Evidence,
                "Plan projection",
                context,
                cancellationToken);
        }

        if (EvalPromptTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? EvalPromptTransitions.Evidence(definition)
                    : validation.Evidence,
                "Eval prompt",
                context,
                cancellationToken);
        }

        if (TraditionalRoadmapPromptTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? TraditionalRoadmapPromptTransitions.Evidence(definition)
                    : validation.Evidence,
                "Traditional roadmap prompt",
                context,
                cancellationToken);
        }

        if (MilestoneDeepDiveTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? MilestoneDeepDiveTransitions.Evidence(definition)
                    : validation.Evidence,
                "Milestone deep-dive",
                context,
                cancellationToken);
        }

        if (PlanReadOnlyReviewTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? PlanReadOnlyReviewTransitions.Evidence(definition)
                    : validation.Evidence,
                "Plan read-only review",
                context,
                cancellationToken);
        }

        if (PlanScopedArtifactTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? PlanScopedArtifactTransitions.Evidence(definition)
                    : validation.Evidence,
                "Plan scoped artifact",
                context,
                cancellationToken);
        }

        if (ExecuteDecisionSessionTransitions.Supports(definition) ||
            ExecuteImplementationTransitions.Supports(definition) ||
            ExecuteRepositoryStateTransitions.Supports(definition) ||
            ExecuteReviewTransitions.Supports(definition))
        {
            return await ExecuteSuccessfulProductEffectsAsync(
                definition,
                validation,
                validation.Evidence.Count == 0
                    ? ExecuteEvidence(definition)
                    : validation.Evidence,
                "Execute transition",
                context,
                cancellationToken);
        }

        if (!LocalVerificationTransitions.Supports(definition))
        {
            return new EffectExecutionResult(
                EffectExecutionStatus.Failed,
                [],
                "Effect execution is not wired because product validation did not run.",
                []);
        }

        WorkflowDefinition workflow = WorkflowFor(definition);
        WorkflowStageDefinition stage = StageFor(workflow, definition);
        IReadOnlyList<string> evidence = validation.Evidence.Count == 0
            ? LocalVerificationTransitions.Evidence(definition)
            : validation.Evidence;
        return await ExecuteSuccessfulProductEffectsAsync(
            definition,
            validation,
            evidence,
            "Local verification",
            context,
            cancellationToken);
    }

    private async Task<EffectExecutionResult> ExecuteSuccessfulProductEffectsAsync(
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        string evidenceTitle,
        EffectExecutionContext context,
        CancellationToken cancellationToken)
    {
        WorkflowDefinition workflow = WorkflowFor(definition);
        WorkflowStageDefinition stage = StageFor(workflow, definition);
        bool executeCommitStalled = await IsExecuteCommitStalledAsync(definition, cancellationToken);
        IReadOnlyList<string> effectEvidence = AddExecuteStallStateEvidence(definition, evidence);
        effectEvidence = await AddFinalClosedStatePersistenceEvidenceAsync(
            definition,
            effectEvidence,
            cancellationToken);
        effectEvidence = await AddTraditionalRoadmapRigorEvidenceAsync(
            workflow,
            stage,
            definition,
            validation,
            effectEvidence,
            context.Causality,
            cancellationToken);
        if (PlanWarmSessionTransitions.Supports(definition))
        {
            await MaterializePlanWarmSessionEvidenceAsync(
                workflow, definition, validation, effectEvidence, context.Causality, cancellationToken);
        }
        else if (PlanProjectionTransitions.Supports(definition))
        {
            await MaterializePlanTransitionEvidenceAsync(
                workflow,
                definition,
                validation,
                effectEvidence,
                PlanProjectionTransitions.Evidence(definition),
                "Plan Projection Evidence",
                context.Causality,
                cancellationToken);
        }
        else if (EvalPromptTransitions.Supports(definition))
        {
            await MaterializePlanTransitionEvidenceAsync(
                workflow,
                definition,
                validation,
                effectEvidence,
                EvalPromptTransitions.Evidence(definition),
                "Eval Prompt Evidence",
                context.Causality,
                cancellationToken);
        }
        else if (TraditionalRoadmapPromptTransitions.Supports(definition))
        {
            await MaterializePlanTransitionEvidenceAsync(
                workflow,
                definition,
                validation,
                effectEvidence,
                TraditionalRoadmapPromptTransitions.Evidence(definition),
                "Traditional Roadmap Prompt Evidence",
                context.Causality,
                cancellationToken);
        }
        else if (MilestoneDeepDiveTransitions.Supports(definition))
        {
            await MaterializePlanTransitionEvidenceAsync(
                workflow,
                definition,
                validation,
                effectEvidence,
                MilestoneDeepDiveTransitions.Evidence(definition),
                "Milestone Deep-Dive Evidence",
                context.Causality,
                cancellationToken);
        }
        else if (PlanReadOnlyReviewTransitions.Supports(definition))
        {
            await MaterializePlanTransitionEvidenceAsync(
                workflow,
                definition,
                validation,
                effectEvidence,
                PlanReadOnlyReviewTransitions.Evidence(definition),
                "Plan Read-Only Review Evidence",
                context.Causality,
                cancellationToken);
        }
        else if (PlanScopedArtifactTransitions.Supports(definition))
        {
            await MaterializePlanScopedArtifactEvidenceAsync(
                workflow, definition, validation, effectEvidence, context.Causality, cancellationToken);
        }
        else if (ExecuteDecisionSessionTransitions.Supports(definition) ||
            ExecuteImplementationTransitions.Supports(definition) ||
            ExecuteRepositoryStateTransitions.Supports(definition) ||
            ExecuteReviewTransitions.Supports(definition))
        {
            await MaterializeExecuteTransitionEvidenceAsync(workflow, definition, validation, effectEvidence, context, cancellationToken);
        }
        else
        {
            await MaterializeLocalVerificationEvidenceAsync(
                workflow, definition, validation, effectEvidence, context.Causality, cancellationToken);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (executeCommitStalled)
        {
            await PersistExecuteStallStateAsync(workflow, stage, definition, effectEvidence, now, cancellationToken);
            EffectExecutionRecord[] stalledRecords = definition.Effects
                .Select(effect => new EffectExecutionRecord(
                    effect.Identity,
                    EffectExecutionStatus.Stalled,
                    $"Execute commit evaluation stalled at `{effect.Identity}`.",
                    effectEvidence))
                .ToArray();
            return new EffectExecutionResult(
                EffectExecutionStatus.Stalled,
                stalledRecords,
                "Execute commit evaluation detected repeated no-substantive-change iterations.",
                effectEvidence);
        }

        var records = new List<EffectExecutionRecord>();
        foreach (EffectDefinition effect in definition.Effects)
        {
            records.Add(new EffectExecutionRecord(
                effect.Identity,
                EffectExecutionStatus.Succeeded,
                $"{evidenceTitle} applied `{effect.Identity}`.",
                effectEvidence));
        }

        return new EffectExecutionResult(
            EffectExecutionStatus.Succeeded,
            records,
            $"{evidenceTitle} effects applied for `{definition.Identity}`.",
            effectEvidence);
    }

    private async Task<bool> IsExecuteCommitStalledAsync(
        WorkflowTransitionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.Identity.Value != "EvaluateCommit")
        {
            return false;
        }

        foreach (string relativePath in ExecuteEvidence(definition))
        {
            string path = ResolveRepositoryPath(relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            string content = await File.ReadAllTextAsync(path, cancellationToken);
            if (content
                .Split(["\r\n", "\n"], StringSplitOptions.None)
                .Any(line => line.Trim().StartsWith("| Stalled |", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("true", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private string[] AddExecuteStallStateEvidence(
        WorkflowTransitionDefinition definition,
        IReadOnlyList<string> evidence)
    {
        const string relativePath = ".LoopRelay/evidence/execute-stall/state.md";
        if (definition.Identity.Value != "EvaluateCommit" ||
            !File.Exists(ResolveRepositoryPath(relativePath)))
        {
            return evidence.ToArray();
        }

        return evidence
            .Concat([relativePath])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private Task<IReadOnlyList<string>> AddFinalClosedStatePersistenceEvidenceAsync(
        WorkflowTransitionDefinition definition,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken)
    {
        if (definition.Identity.Value != "VerifyWorkflowExitGate")
        {
            return Task.FromResult(evidence);
        }

        string relativePath = ".LoopRelay/evidence/execute-completion-recovery/final-closed-state-persistence.md";
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>(evidence
            .Concat([relativePath])
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    private async Task<IReadOnlyList<string>> AddTraditionalRoadmapRigorEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowStageDefinition stage,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        if (workflow.Identity != WorkflowIdentity.TraditionalRoadmap)
        {
            return evidence;
        }

        string relativePath = $".LoopRelay/evidence/traditional-roadmap-effects/{definition.Identity}.md";
        string products = string.Join(", ", validation.Products.Select(product => product.Identity.Value));
        string storage = string.Join(
            ", ",
            validation.Products.SelectMany(product => product.StorageRepresentations).Distinct(StringComparer.Ordinal));
        await ScheduleWriteAsync(
            causality,
            relativePath,
            $"""
            # TraditionalRoadmap Canonical Runtime Evidence

            | Field | Value |
            |---|---|
            | Workflow | {workflow.Identity} |
            | Stage | {stage.Identity} |
            | Transition | {definition.Identity} |
            | Prompt Identity | {definition.PromptIdentity} |
            | Products | {products} |
            | Storage | {storage} |
            | Parser | TraditionalRoadmap transition-specific parser validated the primary output artifact. |
            | Output Validator | {validation.Explanation} |
            | Effects | Product, stage, workflow, effect-record, and evidence persistence are owned by the canonical effect executor. |
            | Warning and Recovery Metadata | Failed, cannot-proceed, cancelled, and partial effect outcomes are recorded through canonical warning and recovery stores. |
            | Transition Ordering | Declared by `{stage.Identity}` stage transition order in the workflow definition. |
            | Prompt Execution Sequencing | Owned by `TransitionRuntime`; prompt success alone does not advance workflow state. |
            | Transition Persistence Sequencing | Started, raw output, interpretation, validation, effects, and completion are persisted by canonical transition stores. |
            | Lifecycle Advancement | Canonical stage and product lifecycle records advance only after output validation and effects. |
            | Next Transition Decisions | Resolved by canonical workflow resolver from products, dependencies, and completed transition runs. |
            | Projection Freshness | Prompt rendering uses generated prompt/catalog source-hash evidence where registered. |
            | Prompt Contract Snapshot | Prompt identity, source-hash evidence, input gate, output gate, and validators are persisted with the transition run. |
            | Input Snapshots | Transition input snapshot hash is persisted by the canonical runtime before prompt execution. |
            | Selection Provenance | Product causal identity and storage representations preserve selected roadmap artifact provenance. |
            | Artifact Promotion Validation | Primary output validation must pass before `PreparedEpic`, `StrategicInitiativeSelection`, or context products become gate-usable. |
            | Decision Ledger | Canonical transition/effect records replace legacy decision-ledger authority for active orchestration. |
            | Split Lineage | Split transition output is represented as canonical product/evidence lineage instead of state-machine control flow. |
            | Warning Evidence | Canonical failed/unsatisfied transition evidence remains repository-owned and resolvable. |
            | Recovery Intent | Canonical recovery markers identify rerun/resume paths without silent repair. |
            | Created At | {DateTimeOffset.UtcNow:O} |
            """,
            cancellationToken);
        return evidence
            .Concat([relativePath])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task PersistExecuteStallStateAsync(
        WorkflowDefinition workflow,
        WorkflowStageDefinition stage,
        WorkflowTransitionDefinition definition,
        IReadOnlyList<string> evidence,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        // Stalls are derived, never latched: the stall evidence is appended as a warning and the
        // next invocation re-evaluates gates without any manual clearing step.
        await _store.AppendWarningAsync(
            new CanonicalWarningRecord(
                CausalUlid.NewId("warn"),
                workflow.Identity,
                stage.Identity,
                definition.Identity,
                WarningCategory.Repository,
                "Execute commit evaluation detected repeated no-substantive-change iterations.",
                "Execute commit gate",
                "Make substantive repository or milestone progress, or rerun after inspecting stall evidence.",
                evidence,
                recordedAt),
            cancellationToken);
    }

    private async Task<EffectExecutionResult> ExecuteLocalArtifactAsync(
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        EffectExecutionContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> evidence = validation.Evidence.Count == 0
            ? LocalArtifactTransitions.Evidence(definition)
            : validation.Evidence;
        try
        {
            await MaterializeLocalArtifactEffectsAsync(
                definition, validation, evidence, context.Causality, cancellationToken);
        }
        catch (Exception exception)
        {
            return new EffectExecutionResult(
                EffectExecutionStatus.Failed,
                definition.Effects.Select(effect => new EffectExecutionRecord(
                    effect.Identity,
                    EffectExecutionStatus.Failed,
                    exception.Message,
                    evidence)).ToArray(),
                exception.Message,
                evidence);
        }

        var records = new List<EffectExecutionRecord>();
        foreach (EffectDefinition effect in definition.Effects)
        {
            records.Add(new EffectExecutionRecord(
                effect.Identity,
                EffectExecutionStatus.Succeeded,
                $"Local artifact effect applied `{effect.Identity}`.",
                evidence));
        }

        return new EffectExecutionResult(
            EffectExecutionStatus.Succeeded,
            records,
            $"Local artifact effects applied for `{definition.Identity}`.",
            evidence);
    }

    private WorkflowDefinition WorkflowFor(WorkflowTransitionDefinition definition) =>
        _definitions.Single(workflow =>
            workflow.Transitions.Any(transition => ReferenceEquals(transition, definition) || transition.Equals(definition)));

    private static WorkflowStageDefinition StageFor(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition) =>
        workflow.Stages.Single(stage => stage.Transitions.Contains(definition.Identity));

    private async Task MaterializeLocalVerificationEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        foreach (string relativePath in LocalVerificationTransitions.Evidence(definition))
        {
            await ScheduleWriteAsync(
                causality,
                relativePath,
                $"""
                # Local Verification Evidence

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                """,
                cancellationToken);
        }
    }

    private async Task MaterializePlanWarmSessionEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        foreach (string relativePath in PlanWarmSessionTransitions.Evidence(definition))
        {
            await ScheduleWriteAsync(
                causality,
                relativePath,
                $"""
                # Plan Warm Session Evidence

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                Evidence: {string.Join(", ", evidence)}
                """,
                cancellationToken);
        }
    }

    private async Task MaterializePlanTransitionEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        IReadOnlyList<string> evidencePaths,
        string title,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        foreach (string relativePath in evidencePaths)
        {
            await ScheduleWriteAsync(
                causality,
                relativePath,
                $"""
                # {title}

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                Evidence: {string.Join(", ", evidence)}
                """,
                cancellationToken);
        }
    }

    private async Task MaterializePlanScopedArtifactEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        foreach (string relativePath in PlanScopedArtifactTransitions.Evidence(definition))
        {
            await ScheduleWriteAsync(
                causality,
                relativePath,
                $"""
                # Plan Scoped Artifact Evidence

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                Evidence: {string.Join(", ", evidence)}
                """,
                cancellationToken);
        }
    }

    private async Task MaterializeExecuteTransitionEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        EffectExecutionContext context,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> loopArtifactEffects =
            await ApplyExecuteLoopArtifactEffectsAsync(definition, context, cancellationToken);
        foreach (string relativePath in ExecuteEvidence(definition))
        {
            await ScheduleWriteAsync(
                context.Causality,
                relativePath,
                $"""
                # Execute Transition Evidence

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                Evidence: {string.Join(", ", evidence)}
                Loop Artifact Effects:
                {RenderLines(loopArtifactEffects)}
                """,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<string>> ApplyExecuteLoopArtifactEffectsAsync(
        WorkflowTransitionDefinition definition,
        EffectExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var artifacts = new LoopArtifacts(
            new RepositoryArtifactStore(new FileSystemArtifactStore(), _repository),
            _repository,
            new LedgerLoopHistoryStore(_repository),
            new CanonicalExecutionRecommendationEvidenceStore(_store));
        var artifactEffects = new DurableLoopArtifactEffectCoordinator(_repository, artifacts);
        CanonicalCausalContext causality = context.Causality;
        return definition.Identity.Value switch
        {
            "GenerateDecision" or "TransferDecisionSession" or "ContinueDecisionSession" =>
                await RotateLiveHandoffEffectAsync(artifactEffects, causality, cancellationToken),
            "GenerateHandoff" => await RetireLiveDecisionsEffectAsync(artifactEffects, causality, cancellationToken),
            "UpdateOperationalContext" => await RotateOperationalDeltaEffectAsync(
                artifactEffects, causality, cancellationToken),
            _ => [],
        };
    }

    private static async Task<IReadOnlyList<string>> RotateLiveHandoffEffectAsync(
        ILoopArtifactEffectCoordinator effects,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        string? content = await effects.RotateLiveHandoffAsync(causality, cancellationToken);
        return content is null
            ? ["No live handoff was present to rotate."]
            : ["Scheduled live handoff rotation through the durable effect plan."];
    }

    private static async Task<IReadOnlyList<string>> RetireLiveDecisionsEffectAsync(
        ILoopArtifactEffectCoordinator effects,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        bool retired = await effects.RetireLiveDecisionsAsync(causality, cancellationToken);
        return retired
            ? ["Scheduled live-decision retirement through the durable effect plan."]
            : ["No live decisions were present to retire."];
    }

    private static async Task<IReadOnlyList<string>> RotateOperationalDeltaEffectAsync(
        ILoopArtifactEffectCoordinator effects,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        string? content = await effects.RotateOperationalDeltaAsync(causality, cancellationToken);
        return content is null
            ? ["No live operational delta was present to rotate."]
            : ["Scheduled operational-delta rotation through the durable effect plan."];
    }

    private static string RenderLines(IReadOnlyList<string> lines) =>
        lines.Count == 0
            ? "- None"
            : string.Join(Environment.NewLine, lines.Select(line => $"- {line}"));

    private async Task MaterializeLocalArtifactEffectsAsync(
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        if (definition.Identity.Value != "GenerateOperationalContext")
        {
            throw new InvalidOperationException($"Unsupported local artifact transition `{definition.Identity}`.");
        }

        string planPath = ResolveRepositoryPath(OrchestrationArtifactPaths.Plan);
        if (!File.Exists(planPath))
        {
            throw new InvalidOperationException($"{OrchestrationArtifactPaths.Plan} was not found.");
        }

        string plan = await File.ReadAllTextAsync(planPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(plan))
        {
            throw new InvalidOperationException($"{OrchestrationArtifactPaths.Plan} is empty.");
        }

        await ScheduleWriteAsync(
            causality, OrchestrationArtifactPaths.OperationalContext, plan, cancellationToken);

        WorkflowDefinition workflow = WorkflowFor(definition);
        await MaterializeLocalArtifactEvidenceAsync(
            workflow, definition, validation, evidence, causality, cancellationToken);
    }

    private async Task MaterializeLocalArtifactEvidenceAsync(
        WorkflowDefinition workflow,
        WorkflowTransitionDefinition definition,
        ProductValidationResult validation,
        IReadOnlyList<string> evidence,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken)
    {
        foreach (string relativePath in LocalArtifactTransitions.Evidence(definition))
        {
            await ScheduleWriteAsync(
                causality,
                relativePath,
                $"""
                # Local Artifact Evidence

                Workflow: {workflow.Identity}
                Transition: {definition.Identity}
                Prompt: {definition.PromptIdentity}
                Status: {validation.Status}
                Explanation: {validation.Explanation}
                Products: {string.Join(", ", validation.Products.Select(product => product.Identity.Value))}
                """,
            cancellationToken);
        }
    }

    private Task ScheduleWriteAsync(
        CanonicalCausalContext causality,
        string relativePath,
        string content,
        CancellationToken cancellationToken) =>
        new DurableFilesystemWriteEffectPlanner(_repository)
            .ScheduleAsync(causality, relativePath, content, cancellationToken);

    private string ResolveRepositoryPath(string relativePath)
    {
        string root = Path.GetFullPath(_repository.Path);
        string path = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Local verification evidence path escaped the repository root.");
        }

        return path;
    }
}
