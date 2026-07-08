using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class InvariantValidator(
    RoadmapArtifacts artifacts,
    ProjectContextLoader projectContextLoader,
    ProjectionRegistry projectionRegistry,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ArtifactLifecycleStore lifecycleStore,
    SplitFamilyStore splitFamilyStore,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private readonly EpicArtifactValidator epicValidator = new();
    private readonly ProjectionProvenanceFactory provenanceFactory = new(projectionRegistry);

    public async Task<InvariantValidationResult> ValidateAsync(
        RoadmapState state,
        string expectedProjectContextHash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            if (!string.Equals(projectContext.Hash, expectedProjectContextHash, StringComparison.Ordinal))
            {
                return await FailAsync(
                    state,
                    RoadmapState.Failed,
                    "ProjectContextDrift",
                    "Project Context hash changed during the run.",
                    "Restore the Project Context evidence to the preflight hash or restart the workflow with the current Project Context.");
            }

            foreach (ProjectionDefinition projection in projectionRegistry.All)
            {
                _ = contractRegistry.Get(projection.RuntimePromptName);
            }

            ProjectionManifest manifest = await manifestStore.LoadAsync();
            foreach (ProjectionDefinition projection in projectionRegistry.All)
            {
                if (!await artifacts.ExistsAsync(projection.ProjectionPath))
                {
                    continue;
                }

                ProjectionManifestEntry? entry = manifest.Find(projection.RuntimePromptName);
                if (entry is null)
                {
                    return await FailAsync(
                        state,
                        RoadmapState.Failed,
                        "ProjectionManifestMissing",
                        $"Projection {projection.ProjectionPath} exists without a manifest entry.",
                        "Restore projection manifest provenance before continuing.");
                }

                if (entry.ValidationStatus == ProjectionValidationStatus.Invalid)
                {
                    return await FailAsync(
                        state,
                        RoadmapState.EvidenceBlocked,
                        "ProjectionInvalid",
                        $"Projection {projection.ProjectionPath} is marked invalid.",
                        "Regenerate or repair the invalid projection evidence before continuing.");
                }

                ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(
                    provenanceFactory.Create(projection, projectContext),
                    entry);
                if (!freshness.IsFresh)
                {
                    return await FailAsync(
                        state,
                        RoadmapState.EvidenceBlocked,
                        "ProjectionProvenanceStale",
                        $"Projection {projection.ProjectionPath} provenance is not fresh: {FormatReasons(freshness.Reasons)}.");
                }
            }

            IReadOnlyList<ArtifactLifecycleEntry> lifecycle = await lifecycleStore.LoadAsync();
            int activeEpics = lifecycle.Count(entry =>
                (entry.State is ArtifactLifecycleState.Ready or ArtifactLifecycleState.Executing) &&
                (string.Equals(entry.Path, RoadmapArtifactPaths.ActiveEpic, StringComparison.OrdinalIgnoreCase) ||
                 entry.Path.StartsWith(".agents/epic-", StringComparison.OrdinalIgnoreCase)));
            if (activeEpics > 1)
            {
                return await FailAsync(
                    state,
                    RoadmapState.Failed,
                    "DuplicateActiveEpic",
                    "More than one epic is marked Ready or Executing.",
                    "Repair artifact lifecycle metadata so exactly one active epic is Ready or Executing.");
            }

            InvariantValidationResult activeEpicResult = await ValidateActiveEpicAsync(state);
            if (!activeEpicResult.IsValid)
            {
                return activeEpicResult;
            }

            InvariantValidationResult executionPreparationResult = await ValidateExecutionPreparationFreshnessAsync(state, cancellationToken);
            if (!executionPreparationResult.IsValid)
            {
                return executionPreparationResult;
            }

            InvariantValidationResult specResult = await ValidateSpecsBelongToActiveEpicAsync(state);
            if (!specResult.IsValid)
            {
                return specResult;
            }

            if (state is RoadmapState.ExecutionPromptReady or RoadmapState.ExecutionLoop)
            {
                if (!await artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic) ||
                    !await artifacts.ExistsAsync(RoadmapArtifactPaths.OperationalContext) ||
                    !await artifacts.ExistsAsync(RoadmapArtifactPaths.ExecutionPrompt))
                {
                    return await FailAsync(
                        state,
                        RoadmapState.EvidenceBlocked,
                        "ExecutionPrerequisitesMissing",
                        "Execution bridge prerequisites are incomplete.",
                        "Restore active epic, operational context, and execution prompt artifacts before continuing.");
                }
            }

            return InvariantValidationResult.Valid();
        }
        catch (RoadmapStepException exception)
        {
            return await FailAsync(state, RoadmapState.EvidenceBlocked, "InvariantEvaluationException", exception.Message);
        }
    }

    public async Task<InvariantValidationResult> ValidateSplitChildPromotionAsync(string childEpicPath)
    {
        try
        {
            if (!SplitEpicBundleInterpreter.IsChildEpicPath(childEpicPath))
            {
                return await FailAsync(
                    RoadmapState.SplitChildSelection,
                    RoadmapState.EvidenceBlocked,
                    "SplitChildPromotionTargetInvalid",
                    $"Split child promotion target is not a valid split child epic path: {childEpicPath}.");
            }

            bool exists = await splitFamilyStore.ExistsForChildAsync(childEpicPath);
            if (!exists)
            {
                return await FailAsync(
                    RoadmapState.SplitChildSelection,
                    RoadmapState.EvidenceBlocked,
                    "SplitFamilyMissing",
                    $"Split child {childEpicPath} has no split-family artifact.");
            }

            string content = await artifacts.ReadRequiredAsync(childEpicPath);
            ArtifactValidationResult validation = epicValidator.Validate(content);
            return validation.IsValid
                ? InvariantValidationResult.Valid()
                : await FailAsync(
                    RoadmapState.SplitChildSelection,
                    RoadmapState.EvidenceBlocked,
                    "SplitChildInvalid",
                    validation.Error ?? $"Split child {childEpicPath} failed epic validation.");
        }
        catch (RoadmapStepException exception)
        {
            return await FailAsync(
                RoadmapState.SplitChildSelection,
                RoadmapState.EvidenceBlocked,
                "SplitChildPromotionException",
                exception.Message);
        }
    }

    private async Task<InvariantValidationResult> ValidateActiveEpicAsync(RoadmapState state)
    {
        if (!RequiresActiveEpic(state))
        {
            return InvariantValidationResult.Valid();
        }

        if (!await artifacts.ExistsAsync(RoadmapArtifactPaths.ActiveEpic))
        {
            return await FailAsync(
                state,
                RoadmapState.EvidenceBlocked,
                "ActiveEpicMissing",
                "Active epic is missing.",
                "Restore or promote an active epic before continuing.");
        }

        string content = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        ArtifactValidationResult validation = epicValidator.Validate(content);
        return validation.IsValid
            ? InvariantValidationResult.Valid()
            : await FailAsync(
                state,
                RoadmapState.EvidenceBlocked,
                "ActiveEpicInvalid",
                validation.Error ?? "Active epic failed validation.",
                "Repair the active epic artifact before continuing.");
    }

    private static bool RequiresActiveEpic(RoadmapState state) =>
        state is RoadmapState.ActiveEpicReady
            or RoadmapState.GenerateMilestoneDeepDives
            or RoadmapState.MilestoneSpecsReady
            or RoadmapState.GenerateOperationalContext
            or RoadmapState.OperationalContextReady
            or RoadmapState.GenerateExecutionPrompt
            or RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop
            or RoadmapState.EpicCompletionDetected
            or RoadmapState.CompletionEvaluationAndContextUpdate;

    private async Task<InvariantValidationResult> ValidateExecutionPreparationFreshnessAsync(
        RoadmapState state,
        CancellationToken cancellationToken)
    {
        bool requireSpecs = RequiresMilestoneSpecs(state);
        bool requireOperationalContext = state is RoadmapState.OperationalContextReady
            or RoadmapState.GenerateExecutionPrompt
            or RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop
            or RoadmapState.EpicCompletionDetected
            or RoadmapState.CompletionEvaluationAndContextUpdate;
        bool requireExecutionPrompt = state is RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop
            or RoadmapState.EpicCompletionDetected
            or RoadmapState.CompletionEvaluationAndContextUpdate;
        bool requireCompatibilityArtifacts = state is RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop;

        if (!requireSpecs && !requireOperationalContext && !requireExecutionPrompt && !requireCompatibilityArtifacts)
        {
            return InvariantValidationResult.Valid();
        }

        ExecutionPreparationReadiness readiness = await executionPreparation.EvaluateReadinessAsync(
            requireSpecs,
            requireOperationalContext,
            requireExecutionPrompt,
            requireCompatibilityArtifacts,
            cancellationToken);
        return readiness.IsFresh
            ? InvariantValidationResult.Valid()
            : await FailAsync(
                state,
                RoadmapState.EvidenceBlocked,
                "ExecutionPreparationStale",
                readiness.Reason,
                "Regenerate stale execution preparation artifacts before continuing.");
    }

    private static bool RequiresMilestoneSpecs(RoadmapState state) =>
        state is RoadmapState.MilestoneSpecsReady
            or RoadmapState.GenerateOperationalContext
            or RoadmapState.OperationalContextReady
            or RoadmapState.GenerateExecutionPrompt
            or RoadmapState.ExecutionPromptReady
            or RoadmapState.ExecutionLoop
            or RoadmapState.EpicCompletionDetected
            or RoadmapState.CompletionEvaluationAndContextUpdate;

    private async Task<InvariantValidationResult> ValidateSpecsBelongToActiveEpicAsync(RoadmapState state)
    {
        IReadOnlyList<string> specs = RequiresMilestoneSpecs(state)
            ? await executionPreparation.RequireFreshMilestoneSpecPathsAsync()
            : [];
        foreach (string spec in specs)
        {
            string content = await artifacts.ReadRequiredAsync(spec);
            string? declaredEpicPath = FindDeclaredEpicPath(content);
            if (declaredEpicPath is not null &&
                !string.Equals(declaredEpicPath, RoadmapArtifactPaths.ActiveEpic, StringComparison.OrdinalIgnoreCase))
            {
                return await FailAsync(
                    state,
                    RoadmapState.EvidenceBlocked,
                    "SpecEpicMismatch",
                    $"{spec} belongs to {declaredEpicPath}, not the active epic.",
                    "Repair milestone spec provenance so all active specs belong to the active epic.");
            }
        }

        return InvariantValidationResult.Valid();
    }

    private static string? FindDeclaredEpicPath(string content)
    {
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("| Epic Path |", StringComparison.OrdinalIgnoreCase))
            {
                string[] cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
                return cells.Length >= 2 ? cells[1] : null;
            }

            if (trimmed.StartsWith("Epic Path:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Epic Path:".Length..].Trim();
            }
        }

        return null;
    }

    private async Task<InvariantValidationResult> FailAsync(
        RoadmapState currentState,
        RoadmapState failureState,
        string category,
        string message,
        string recoveryGuidance = "Repair the invariant violation before continuing the roadmap state machine.")
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string details = $"""
            Invariant validation failed before the workflow state machine could safely continue.

            | Field | Value |
            |---|---|
            | Validated State | {currentState} |
            | Failure State | {failureState} |
            | Invariant Category | {category} |
            | Recovery Guidance | {recoveryGuidance} |

            ## Diagnostic

            {message}
            """;
        string content = RoadmapBlockedArtifact.Render(
            failureState,
            $"InvariantValidator:{category}",
            message,
            recoveryGuidance,
            "None",
            details,
            createdAt);
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.OrchestrationEvidenceDirectory,
            "invariant-failure",
            content);
        return InvariantValidationResult.Invalid(failureState, message, path, category, recoveryGuidance);
    }

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}
