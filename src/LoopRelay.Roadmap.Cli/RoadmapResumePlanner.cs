namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapResumePlanner(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ArtifactLifecycleStore lifecycleStore,
    ProjectionProvenanceFactory provenanceFactory,
    SelectionProvenanceService selectionProvenance,
    ExecutionPreparationProvenanceService executionPreparation)
{
    private readonly EpicArtifactValidator epicValidator = new();

    public async Task<RoadmapResumePlan> PlanAsync(
        RoadmapStateDocument? persistedState,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RoadmapArtifactSnapshot snapshot = await RoadmapArtifactSnapshot.CaptureAsync(
            artifacts,
            manifestStore,
            lifecycleStore,
            executionPreparation);

        if (persistedState is null)
        {
            return RoadmapResumePlan.InitializeCoreReady("No persisted roadmap state exists.");
        }

        if (RoadmapWorkflowStateClassifier.IsReportOnlyState(persistedState.CurrentState))
        {
            return RoadmapResumePlan.Terminal(
                RoadmapWorkflowStateClassifier.ReportOutcome(persistedState.CurrentState),
                persistedState.CurrentState,
                RoadmapWorkflowStateClassifier.ReportReason(persistedState.CurrentState));
        }

        if (persistedState.CurrentState == RoadmapState.Cancelled)
        {
            RoadmapState recoveryState = CancelledRecoveryState(persistedState);
            RoadmapResumePlan recovery = await PlanForStateAsync(
                recoveryState,
                snapshot,
                projectContext,
                persistedState.RetiredEpics,
                cancellationToken);
            return recovery with
            {
                SourceState = persistedState.CurrentState,
                Reason = $"Recovering cancelled workflow from {recoveryState}: {recovery.Reason}",
            };
        }

        ResumeSafety transitionSafety = ValidateIncompleteTransition(persistedState, snapshot);
        if (!transitionSafety.IsSafe)
        {
            return RoadmapResumePlan.Block(
                persistedState.CurrentState,
                transitionSafety.Reason);
        }

        ResumeSafety projectionSafety = ValidateProjectionForLastTransition(persistedState, snapshot, projectContext);
        if (!projectionSafety.IsSafe)
        {
            return RoadmapResumePlan.Block(
                persistedState.CurrentState,
                projectionSafety.Reason);
        }

        return await PlanForStateAsync(
            persistedState.CurrentState,
            snapshot,
            projectContext,
            persistedState.RetiredEpics,
            cancellationToken);
    }

    private async Task<RoadmapResumePlan> PlanForStateAsync(
        RoadmapState state,
        RoadmapArtifactSnapshot snapshot,
        ProjectContext projectContext,
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (state)
        {
            case RoadmapState.CoreReady:
            case RoadmapState.BootstrapRoadmapCompletionContext:
                return RoadmapResumePlan.ContinueFromCoreReady(
                    state,
                    "Core is ready; completion-context readiness determines the next transition.");

            case RoadmapState.RoadmapCompletionContextReady:
            case RoadmapState.RetireEpic:
            {
                ResumeSafety safety = ValidatePromptReadiness(
                    "SelectNextEpic",
                    snapshot,
                    projectContext,
                    requireOutputs: false);
                return safety.IsSafe
                    ? RoadmapResumePlan.SelectNextStrategicInitiative(state, safety.Reason)
                    : RoadmapResumePlan.Block(state, safety.Reason);
            }

            case RoadmapState.SelectNextStrategicInitiative:
            {
                if (snapshot.HasPresentFile(RoadmapArtifactPaths.Selection))
                {
                    ResumeSafety activeSelection = await ValidateActiveSelectionFreshnessAsync(
                        retiredEpics,
                        cancellationToken);
                    if (activeSelection.IsSafe)
                    {
                        if (!snapshot.HasUsableFile(RoadmapArtifactPaths.Selection))
                        {
                            ResumeSafety archivedSelectionReadiness = ValidatePromptReadiness(
                                "SelectNextEpic",
                                snapshot,
                                projectContext,
                                requireOutputs: false);
                            return archivedSelectionReadiness.IsSafe
                                ? RoadmapResumePlan.SelectNextStrategicInitiative(
                                    state,
                                    "Active selection provenance is current, but the selection artifact lifecycle is not usable. A fresh selection will be generated.")
                                : RoadmapResumePlan.Block(state, archivedSelectionReadiness.Reason);
                        }

                        ResumeSafety completedSelection = ValidatePromptReadiness(
                            "SelectNextEpic",
                            snapshot,
                            projectContext,
                            requireOutputs: true);
                        return completedSelection.IsSafe
                            ? RoadmapResumePlan.ContinueSelectionDecision(state, completedSelection.Reason)
                            : RoadmapResumePlan.Block(state, completedSelection.Reason);
                    }

                    ResumeSafety regenerationReadiness = ValidatePromptReadiness(
                        "SelectNextEpic",
                        snapshot,
                        projectContext,
                        requireOutputs: false);
                    return regenerationReadiness.IsSafe
                        ? RoadmapResumePlan.SelectNextStrategicInitiative(
                            state,
                            $"{activeSelection.Reason} A fresh selection will be generated.")
                        : RoadmapResumePlan.Block(state, regenerationReadiness.Reason);
                }

                ResumeSafety selectionReadiness = ValidatePromptReadiness(
                    "SelectNextEpic",
                    snapshot,
                    projectContext,
                    requireOutputs: false);
                return selectionReadiness.IsSafe
                    ? RoadmapResumePlan.SelectNextStrategicInitiative(state, selectionReadiness.Reason)
                    : RoadmapResumePlan.Block(state, selectionReadiness.Reason);
            }

            case RoadmapState.ActiveEpicReady:
            case RoadmapState.GenerateMilestoneDeepDives:
            {
                ResumeSafety safety = await ValidateExecutionPreparationAsync(
                    RoadmapState.ActiveEpicReady,
                    snapshot,
                    requireSpecs: false,
                    requireOperationalContext: false,
                    requireExecutionPrompt: false,
                    cancellationToken);
                if (!safety.IsSafe)
                {
                    return RoadmapResumePlan.Block(state, safety.Reason);
                }

                ResumeSafety promptSafety = ValidatePromptReadiness(
                    "GenerateMilestoneDeepDivesForEpic",
                    snapshot,
                    projectContext,
                    requireOutputs: false);
                return promptSafety.IsSafe
                    ? RoadmapResumePlan.PrepareExecutionFromActiveEpic(state, promptSafety.Reason)
                    : RoadmapResumePlan.Block(state, promptSafety.Reason);
            }

            case RoadmapState.MilestoneSpecsReady:
            case RoadmapState.GenerateOperationalContext:
            {
                ResumeSafety safety = await ValidateExecutionPreparationAsync(
                    RoadmapState.MilestoneSpecsReady,
                    snapshot,
                    requireSpecs: true,
                    requireOperationalContext: false,
                    requireExecutionPrompt: false,
                    cancellationToken);
                return safety.IsSafe
                    ? RoadmapResumePlan.PrepareExecutionFromMilestoneSpecs(state, safety.Reason)
                    : RoadmapResumePlan.Block(state, safety.Reason);
            }

            case RoadmapState.OperationalContextReady:
            case RoadmapState.GenerateExecutionPrompt:
            {
                ResumeSafety safety = await ValidateExecutionPreparationAsync(
                    RoadmapState.OperationalContextReady,
                    snapshot,
                    requireSpecs: true,
                    requireOperationalContext: true,
                    requireExecutionPrompt: false,
                    cancellationToken);
                return safety.IsSafe
                    ? RoadmapResumePlan.PrepareExecutionFromOperationalContext(state, safety.Reason)
                    : RoadmapResumePlan.Block(state, safety.Reason);
            }

            case RoadmapState.ExecutionPromptReady:
            case RoadmapState.ExecutionLoop:
            {
                ResumeSafety safety = await ValidateExecutionPreparationAsync(
                    RoadmapState.ExecutionPromptReady,
                    snapshot,
                    requireSpecs: true,
                    requireOperationalContext: true,
                    requireExecutionPrompt: true,
                    cancellationToken);
                return safety.IsSafe
                    ? RoadmapResumePlan.RunExecution(state, safety.Reason)
                    : RoadmapResumePlan.Block(state, safety.Reason);
            }

            case RoadmapState.EpicCompletionDetected:
                return RoadmapResumePlan.EvaluateCompletionClaim(
                    state,
                    "Execution completion claim is persisted; continue completion certification.");

            case RoadmapState.StrategicInvestigationRequired:
            case RoadmapState.RoadmapRevisionRequired:
            case RoadmapState.NoSuitableInitiative:
            case RoadmapState.EvidenceGathering:
            case RoadmapState.ExecutionBlocked:
                return RoadmapResumePlan.Terminal(
                    RoadmapOutcome.Paused,
                    state,
                    $"Persisted roadmap state is paused at {state}.");

            default:
                return RoadmapResumePlan.Block(
                    state,
                    $"No safe resume rule is registered for persisted roadmap state {state}.");
        }
    }

    private ResumeSafety ValidatePromptReadiness(
        string runtimePrompt,
        RoadmapArtifactSnapshot snapshot,
        ProjectContext projectContext,
        bool requireOutputs)
    {
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ResumeSafety projectionSafety = ValidateProjection(runtimePrompt, snapshot, projectContext);
        if (!projectionSafety.IsSafe)
        {
            return projectionSafety;
        }

        foreach (string input in contract.RequiredInputs)
        {
            if (!snapshot.HasRequiredInput(input))
            {
                return ResumeSafety.Unsafe(
                    $"Required input `{input}` for {runtimePrompt} is not artifact-ready.");
            }
        }

        if (requireOutputs)
        {
            foreach (string output in contract.RequiredOutputs)
            {
                if (!snapshot.HasRequiredOutput(output))
                {
                    return ResumeSafety.Unsafe(
                        $"Required output `{output}` for completed {runtimePrompt} is not artifact-ready.");
                }
            }
        }

        return ResumeSafety.Safe($"{runtimePrompt} contract inputs are artifact-ready.");
    }

    private ResumeSafety ValidateProjection(
        string runtimePrompt,
        RoadmapArtifactSnapshot snapshot,
        ProjectContext projectContext)
    {
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionManifestEntry? entry = snapshot.Manifest.Find(runtimePrompt);
        if (entry is null)
        {
            return ResumeSafety.Safe($"{runtimePrompt} has no existing projection manifest entry.");
        }

        if (entry.ValidationStatus == ProjectionValidationStatus.Invalid)
        {
            return ResumeSafety.Unsafe(
                $"Projection manifest entry for {runtimePrompt} is invalid: {entry.LastValidationError ?? "no validation detail"}.");
        }

        ProjectionFreshness freshness = ProjectionFreshnessEvaluator.Evaluate(
            provenanceFactory.Create(runtimePrompt, projectContext),
            entry);
        if (contract.StaleProjectionPolicy == StaleProjectionPolicy.Block && !freshness.IsFresh)
        {
            return ResumeSafety.Unsafe(
                $"Projection manifest entry for {runtimePrompt} is stale: {FormatReasons(freshness.Reasons)}.");
        }

        return ResumeSafety.Safe($"{runtimePrompt} projection manifest entry is usable.");
    }

    private async Task<ResumeSafety> ValidateActiveSelectionFreshnessAsync(
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken)
    {
        string? projectionContent = await artifacts.ReadAsync(RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"]);
        if (string.IsNullOrWhiteSpace(projectionContent))
        {
            return ResumeSafety.Unsafe("Active selection cannot be reused because the SelectNextEpic projection is missing.");
        }

        TransitionInputSnapshot currentCycle = await selectionProvenance.CaptureCurrentCycleAsync(
            projectionContent,
            retiredEpics,
            cancellationToken);
        DerivedArtifactFreshness freshness = await selectionProvenance.EvaluateActiveSelectionFreshnessAsync(
            currentCycle,
            retiredEpics,
            cancellationToken);
        return freshness.IsFresh
            ? ResumeSafety.Safe("Active selection belongs to the current selection cycle.")
            : ResumeSafety.Unsafe($"Active selection does not belong to the current selection cycle: {FormatDerivedReasons(freshness.Reasons)}.");
    }

    private ResumeSafety ValidateProjectionForLastTransition(
        RoadmapStateDocument persistedState,
        RoadmapArtifactSnapshot snapshot,
        ProjectContext projectContext)
    {
        string prompt = persistedState.LastTransition.Prompt;
        if (string.IsNullOrWhiteSpace(prompt) ||
            string.Equals(prompt, "None", StringComparison.OrdinalIgnoreCase) ||
            !contractRegistry.Contains(prompt))
        {
            return ResumeSafety.Safe("Last transition has no runtime prompt contract to validate.");
        }

        return ValidateProjection(prompt, snapshot, projectContext);
    }

    private ResumeSafety ValidateIncompleteTransition(
        RoadmapStateDocument persistedState,
        RoadmapArtifactSnapshot snapshot)
    {
        if (persistedState.LastTransition.Status is not (TransitionStatus.Started or TransitionStatus.PromptCompleted))
        {
            return ResumeSafety.Safe("Persisted last transition is not incomplete.");
        }

        IReadOnlyList<string> outputPaths = ParseOutputPaths(persistedState.LastTransition.Output);
        if (outputPaths.Count == 0)
        {
            return ResumeSafety.Unsafe(
                $"Persisted transition {persistedState.LastTransition.Prompt} is {persistedState.LastTransition.Status} with no durable output path.");
        }

        string[] missing = outputPaths
            .Where(path => !snapshot.HasRequiredOutput(path))
            .ToArray();
        return missing.Length == 0
            ? ResumeSafety.Safe("Incomplete transition has durable outputs and can be interpreted by state.")
            : ResumeSafety.Unsafe(
                $"Persisted transition {persistedState.LastTransition.Prompt} is {persistedState.LastTransition.Status}, but output artifacts are not ready: {string.Join(", ", missing)}.");
    }

    private async Task<ResumeSafety> ValidateExecutionPreparationAsync(
        RoadmapState state,
        RoadmapArtifactSnapshot snapshot,
        bool requireSpecs,
        bool requireOperationalContext,
        bool requireExecutionPrompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!snapshot.HasUsableActiveEpic())
        {
            return ResumeSafety.Unsafe("Active epic is missing or its lifecycle state is not usable.");
        }

        string? activeEpic = await artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic);
        ArtifactValidationResult epicValidation = epicValidator.Validate(activeEpic ?? string.Empty);
        if (!epicValidation.IsValid)
        {
            return ResumeSafety.Unsafe(epicValidation.Error ?? "Active epic failed validation.");
        }

        if (requireSpecs || requireOperationalContext || requireExecutionPrompt)
        {
            ExecutionPreparationReadiness readiness = await executionPreparation.EvaluateReadinessAsync(
                requireSpecs,
                requireOperationalContext,
                requireExecutionPrompt,
                requireCompatibilityArtifacts: false,
                cancellationToken);
            if (!readiness.IsFresh)
            {
                return ResumeSafety.Unsafe(readiness.Reason);
            }
        }

        if (requireSpecs)
        {
            ResumeSafety specSafety = await ValidateSpecsAsync(cancellationToken);
            if (!specSafety.IsSafe)
            {
                return specSafety;
            }
        }

        return ResumeSafety.Safe($"{state} artifacts are ready.");
    }

    private async Task<ResumeSafety> ValidateSpecsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> milestoneSpecPaths;
        try
        {
            milestoneSpecPaths = await executionPreparation.RequireFreshMilestoneSpecPathsAsync(cancellationToken);
        }
        catch (RoadmapStepException exception)
        {
            return ResumeSafety.Unsafe(exception.Message);
        }

        if (milestoneSpecPaths.Count == 0)
        {
            return ResumeSafety.Unsafe("Milestone specs are missing.");
        }

        foreach (string spec in milestoneSpecPaths)
        {
            string content = await artifacts.ReadRequiredAsync(spec);
            string? declaredEpicPath = FindDeclaredEpicPath(content);
            if (declaredEpicPath is not null &&
                !string.Equals(declaredEpicPath, RoadmapArtifactPaths.ActiveEpic, StringComparison.OrdinalIgnoreCase))
            {
                return ResumeSafety.Unsafe($"{spec} belongs to {declaredEpicPath}, not the active epic.");
            }
        }

        return ResumeSafety.Safe("Milestone specs are ready.");
    }

    private static RoadmapState CancelledRecoveryState(RoadmapStateDocument persistedState)
    {
        if (persistedState.TransitionIntent.Intent != "None" &&
            persistedState.TransitionIntent.DispatchState != RoadmapState.Cancelled)
        {
            return persistedState.TransitionIntent.DispatchState;
        }

        return persistedState.LastTransition.From == RoadmapState.Cancelled
            ? RoadmapState.CoreReady
            : persistedState.LastTransition.From;
    }

    private static IReadOnlyList<string> ParseOutputPaths(string output)
    {
        if (string.IsNullOrWhiteSpace(output) ||
            string.Equals(output.Trim(), "None", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return output
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.Equals(path, "None", StringComparison.OrdinalIgnoreCase))
            .ToArray();
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

    private static string FormatReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);

    private static string FormatDerivedReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);
}

internal sealed record RoadmapResumePlan(
    RoadmapResumeAction Action,
    RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? TerminalOutcome = null,
    bool ShouldPersistCoreReady = false)
{
    public static RoadmapResumePlan InitializeCoreReady(string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, RoadmapState.CoreReady, reason, ShouldPersistCoreReady: true);

    public static RoadmapResumePlan ContinueFromCoreReady(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, sourceState, reason);

    public static RoadmapResumePlan SelectNextStrategicInitiative(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.SelectNextStrategicInitiative, sourceState, reason);

    public static RoadmapResumePlan ContinueSelectionDecision(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueSelectionDecision, sourceState, reason);

    public static RoadmapResumePlan PrepareExecutionFromActiveEpic(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.PrepareExecutionFromActiveEpic, sourceState, reason);

    public static RoadmapResumePlan PrepareExecutionFromMilestoneSpecs(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.PrepareExecutionFromMilestoneSpecs, sourceState, reason);

    public static RoadmapResumePlan PrepareExecutionFromOperationalContext(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.PrepareExecutionFromOperationalContext, sourceState, reason);

    public static RoadmapResumePlan RunExecution(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.RunExecution, sourceState, reason);

    public static RoadmapResumePlan EvaluateCompletionClaim(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.EvaluateCompletionClaim, sourceState, reason);

    public static RoadmapResumePlan Terminal(RoadmapOutcome outcome, RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Terminal, sourceState, reason, outcome);

    public static RoadmapResumePlan Block(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Block, sourceState, reason, RoadmapOutcome.Paused);
}

internal enum RoadmapResumeAction
{
    ContinueFromCoreReady,
    SelectNextStrategicInitiative,
    ContinueSelectionDecision,
    PrepareExecutionFromActiveEpic,
    PrepareExecutionFromMilestoneSpecs,
    PrepareExecutionFromOperationalContext,
    RunExecution,
    EvaluateCompletionClaim,
    Terminal,
    Block,
}

internal sealed record ResumeSafety(bool IsSafe, string Reason)
{
    public static ResumeSafety Safe(string reason) => new(true, reason);

    public static ResumeSafety Unsafe(string reason) => new(false, reason);
}

internal sealed class RoadmapArtifactSnapshot
{
    private RoadmapArtifactSnapshot(
        IReadOnlyDictionary<string, ArtifactStatus> statuses,
        IReadOnlyDictionary<string, ArtifactLifecycleState> lifecycle,
        IReadOnlyDictionary<string, int> directoryArtifactCounts,
        IReadOnlyList<string> milestoneSpecPaths,
        bool roadmapSourceAvailable,
        ProjectionManifest manifest)
    {
        Statuses = statuses;
        Lifecycle = lifecycle;
        DirectoryArtifactCounts = directoryArtifactCounts;
        MilestoneSpecPaths = milestoneSpecPaths;
        RoadmapSourceAvailable = roadmapSourceAvailable;
        Manifest = manifest;
    }

    public IReadOnlyDictionary<string, ArtifactStatus> Statuses { get; }
    public IReadOnlyDictionary<string, ArtifactLifecycleState> Lifecycle { get; }
    public IReadOnlyDictionary<string, int> DirectoryArtifactCounts { get; }
    public IReadOnlyList<string> MilestoneSpecPaths { get; }
    public bool RoadmapSourceAvailable { get; }
    public ProjectionManifest Manifest { get; }

    public static async Task<RoadmapArtifactSnapshot> CaptureAsync(
        RoadmapArtifacts artifacts,
        ProjectionManifestStore manifestStore,
        ArtifactLifecycleStore lifecycleStore,
        ExecutionPreparationProvenanceService executionPreparation)
    {
        string[] knownPaths =
        [
            RoadmapArtifactPaths.StateJson,
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.Selection,
            RoadmapArtifactPaths.ActiveEpic,
            RoadmapArtifactPaths.OperationalContext,
            RoadmapArtifactPaths.ExecutionPrompt,
            RoadmapArtifactPaths.DecisionLedgerJson,
        ];

        var statuses = new Dictionary<string, ArtifactStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in knownPaths)
        {
            statuses[path] = await artifacts.GetStatusAsync(path);
        }

        IReadOnlyList<string> specs = await FreshMilestoneSpecPathsOrEmptyAsync(executionPreparation);
        foreach (string spec in specs)
        {
            statuses[spec] = await artifacts.GetStatusAsync(spec);
        }

        IReadOnlyList<string> roadmapFiles = await artifacts.ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        bool roadmapSourceAvailable = roadmapFiles.Count > 0;

        IReadOnlyDictionary<string, ArtifactLifecycleState> lifecycle = (await lifecycleStore.LoadAsync())
            .GroupBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().State,
                StringComparer.OrdinalIgnoreCase);

        var directoryArtifactCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [RoadmapArtifactPaths.SpecsDirectory] = specs.Count,
            [RoadmapArtifactPaths.SelectionEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.AuditEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.ExecutionEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.ExecutionEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.EvaluationEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.BlockerEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.SplitFamiliesDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "*.json")).Count,
        };

        return new RoadmapArtifactSnapshot(
            statuses,
            lifecycle,
            directoryArtifactCounts,
            specs,
            roadmapSourceAvailable,
            await manifestStore.LoadAsync());
    }

    private static async Task<IReadOnlyList<string>> FreshMilestoneSpecPathsOrEmptyAsync(
        ExecutionPreparationProvenanceService executionPreparation)
    {
        try
        {
            return await executionPreparation.RequireFreshMilestoneSpecPathsAsync();
        }
        catch (RoadmapStepException)
        {
            return [];
        }
    }

    public bool HasRequiredInput(string path)
    {
        if (string.Equals(path, RoadmapArtifactPaths.RoadmapDirectoryPattern, StringComparison.OrdinalIgnoreCase))
        {
            return RoadmapSourceAvailable;
        }

        if (string.Equals(path, RoadmapArtifactPaths.SpecsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return MilestoneSpecPaths.Count > 0;
        }

        return HasUsableFile(path);
    }

    public bool HasRequiredOutput(string path)
    {
        if (string.Equals(path, RoadmapArtifactPaths.SpecsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return MilestoneSpecPaths.Count > 0;
        }

        if (DirectoryArtifactCounts.TryGetValue(path, out int artifactCount))
        {
            return artifactCount > 0;
        }

        return HasUsableFile(path);
    }

    public bool HasUsableActiveEpic()
    {
        if (!HasPresentFile(RoadmapArtifactPaths.ActiveEpic))
        {
            return false;
        }

        if (!Lifecycle.TryGetValue(RoadmapArtifactPaths.ActiveEpic, out ArtifactLifecycleState state))
        {
            return true;
        }

        return state is ArtifactLifecycleState.Ready or ArtifactLifecycleState.Executing;
    }

    public bool HasUsableFile(string path) =>
        HasPresentFile(path) && LifecycleAllowsUse(path);

    public bool HasPresentFile(string path) =>
        Statuses.TryGetValue(path, out ArtifactStatus status) && status == ArtifactStatus.Present;

    private bool LifecycleAllowsUse(string path)
    {
        if (!Lifecycle.TryGetValue(path, out ArtifactLifecycleState state))
        {
            return true;
        }

        return state is ArtifactLifecycleState.Ready
            or ArtifactLifecycleState.Executing
            or ArtifactLifecycleState.Completed;
    }
}
