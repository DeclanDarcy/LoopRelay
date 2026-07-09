using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.Execution;
using LoopRelay.Roadmap.Cli.Primitives.Projections;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Execution;
using LoopRelay.Roadmap.Cli.Services.Persistence;

namespace LoopRelay.Roadmap.Cli.Services.TransitionCoordination;

internal sealed class RoadmapTransitionPersistence(
    RoadmapArtifacts _artifacts,
    IProjectionManifestStore _manifestStore,
    IRoadmapStateStore _stateStore,
    IDecisionLedgerStore _decisionLedger,
    ITransitionJournalStore _journalStore,
    ISplitFamilyStore _splitFamilyStore,
    IWorkflowPersistenceCoordinator? workflowCoordinator = null)
{
    private readonly IWorkflowPersistenceCoordinator _workflowCoordinator =
        workflowCoordinator ?? NullWorkflowPersistenceCoordinator.Instance;

    public async Task SaveAsync(
        RoadmapState current,
        TransitionStatus status,
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projection,
        string output,
        string decision,
        DateTimeOffset started,
        DateTimeOffset? completed,
        IReadOnlyList<RetiredEpic>? retiredEpics,
        IReadOnlyList<BlockerRow>? blockers,
        RoadmapTransitionIntent? transitionIntent = null,
        IReadOnlyList<string>? nextTransitions = null)
    {
        await _workflowCoordinator.ExecuteAsync(
            _artifacts.Repository,
            WorkflowPersistenceUnit.RoadmapTransitionSave,
            $"{prompt}:{started:O}",
            _ => SaveCoreAsync(
                current,
                status,
                from,
                to,
                prompt,
                projection,
                output,
                decision,
                started,
                completed,
                retiredEpics,
                blockers,
                transitionIntent,
                nextTransitions));
    }

    private async Task SaveCoreAsync(
        RoadmapState current,
        TransitionStatus status,
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projection,
        string output,
        string decision,
        DateTimeOffset started,
        DateTimeOffset? completed,
        IReadOnlyList<RetiredEpic>? retiredEpics,
        IReadOnlyList<BlockerRow>? blockers,
        RoadmapTransitionIntent? transitionIntent,
        IReadOnlyList<string>? nextTransitions)
    {
        RoadmapStateDocument? existing = await _stateStore.LoadAsync();
        RoadmapStateSummarySnapshot summary = await CaptureSummaryAsync();
        IReadOnlyList<RetiredEpic> effectiveRetiredEpics = retiredEpics ?? existing?.RetiredEpics ?? [];
        IReadOnlyList<BlockerRow> effectiveBlockers = blockers ?? existing?.Blockers ?? [];

        await _stateStore.SaveAsync(new RoadmapStateDocument(
            current,
            summary.ActiveArtifacts,
            new RoadmapTransitionSummary(from, to, prompt, projection, output, decision, status, started, completed),
            effectiveBlockers,
            summary.LastDecisionId,
            effectiveRetiredEpics.Count,
            summary.SplitFamiliesCount,
            summary.ProjectionManifestCounts,
            transitionIntent ?? existing?.TransitionIntent ?? RoadmapTransitionIntent.Empty(current),
            nextTransitions ?? NextTransitions(current),
            effectiveRetiredEpics));
    }

    public async Task RecordDecisionAndSaveAsync(
        RoadmapState current,
        TransitionStatus status,
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projection,
        string output,
        string decision,
        string confidence,
        string rationale,
        DateTimeOffset started,
        DateTimeOffset? completed,
        IReadOnlyList<RetiredEpic>? retiredEpics,
        IReadOnlyList<BlockerRow>? blockers,
        RoadmapTransitionIntent? transitionIntent = null,
        IReadOnlyList<string>? nextTransitions = null)
    {
        await _workflowCoordinator.ExecuteAsync(
            _artifacts.Repository,
            WorkflowPersistenceUnit.DecisionRecordingAndStateUpdate,
            $"{prompt}:{started:O}",
            async _ =>
            {
                string id = await _decisionLedger.NextDecisionIdAsync();
                await _decisionLedger.AppendAsync(new DecisionLedgerEntry(
                    id,
                    DateTimeOffset.UtcNow,
                    current,
                    prompt,
                    prompt,
                    projection,
                    [],
                    [output],
                    decision,
                    confidence,
                    rationale));
                await SaveCoreAsync(
                    current,
                    status,
                    from,
                    to,
                    prompt,
                    projection,
                    output,
                    decision,
                    started,
                    completed,
                    retiredEpics,
                    blockers,
                    transitionIntent,
                    nextTransitions);
            });
    }

    public async Task RefreshAndSaveAsync(RoadmapStateDocument document)
    {
        await _workflowCoordinator.ExecuteAsync(
            _artifacts.Repository,
            WorkflowPersistenceUnit.RoadmapTransitionSave,
            $"{document.CurrentState}:{DateTimeOffset.UtcNow:O}",
            _ => RefreshAndSaveCoreAsync(document));
    }

    private async Task RefreshAndSaveCoreAsync(RoadmapStateDocument document)
    {
        RoadmapStateSummarySnapshot summary = await CaptureSummaryAsync();
        await _stateStore.SaveAsync(document with
        {
            ActiveArtifacts = summary.ActiveArtifacts,
            LastDecisionId = summary.LastDecisionId,
            RetiredEpicsCount = document.RetiredEpics.Count,
            SplitFamiliesCount = summary.SplitFamiliesCount,
            ProjectionManifestCounts = summary.ProjectionManifestCounts,
        });
    }

    public async Task PersistWorkflowFailureAsync(RoadmapWorkflowFailure failure)
    {
        await _workflowCoordinator.ExecuteAsync(
            _artifacts.Repository,
            WorkflowPersistenceUnit.DecisionRecordingAndStateUpdate,
            $"{failure.JournalEvent}:{failure.FailedAt:O}",
            _ => PersistWorkflowFailureCoreAsync(failure));
    }

    private async Task PersistWorkflowFailureCoreAsync(RoadmapWorkflowFailure failure)
    {
        IReadOnlyDictionary<string, string> inputHashes = failure.InputSnapshot?.ToInputArtifactHashes()
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        await _journalStore.AppendAsync(new TransitionJournalRecord(
            failure.JournalEvent,
            Guid.NewGuid().ToString("N"),
            failure.FailedAt,
            failure.OriginatingState,
            failure.AttemptedState,
            failure.Transition,
            failure.Projection,
            failure.PromptContractKey,
            inputHashes,
            failure.EvidencePaths,
            0,
            failure.FailureState.ToString(),
            failure.FailureCategory,
            failure.Reason,
            failure.InputSnapshot));

        await SaveCoreAsync(
            failure.FailureState,
            failure.StateTransitionStatus,
            failure.OriginatingState,
            failure.AttemptedState,
            failure.Transition,
            failure.Projection,
            FormatList(failure.EvidencePaths),
            failure.Decision,
            failure.FailedAt,
            failure.FailedAt,
            null,
            [new BlockerRow(failure.Reason, failure.RequiredNextStep)],
            new RoadmapTransitionIntent(failure.RecoveryIntent, failure.FailureState, failure.EvidencePaths),
            ["Review invariant failure evidence and rerun"]);
    }

    public async Task PersistInvariantFailureAndThrowAsync(
        InvariantValidationResult invariant,
        RoadmapState originatingState,
        RoadmapState attemptedState,
        string transition,
        string projection)
    {
        string reason = invariant.Error ?? "Invariant validation failed.";
        DateTimeOffset failedAt = DateTimeOffset.UtcNow;
        IReadOnlyList<string> evidencePaths = await EnsureInvariantEvidencePathsAsync(
            invariant,
            transition,
            attemptedState,
            reason,
            failedAt);
        RoadmapWorkflowFailure failure = RoadmapWorkflowFailure.InvariantFailure(
            originatingState,
            attemptedState,
            invariant.FailureState,
            transition,
            projection,
            invariant.FailureCategory,
            evidencePaths,
            reason,
            invariant.RecoveryGuidance,
            failedAt);

        await PersistWorkflowFailureAsync(failure);
        throw RoadmapStepException.AlreadyPersisted(new RoadmapStepException(reason));
    }

    public static IReadOnlyList<string> ParseOutputEvidencePaths(string output)
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

    private async Task<RoadmapStateSummarySnapshot> CaptureSummaryAsync()
    {
        ProjectionManifest manifest = await _manifestStore.LoadAsync();
        IReadOnlyList<ArtifactStateRow> activeArtifacts = await ActiveArtifactRowsAsync();
        string lastDecision = await _decisionLedger.LastDecisionIdAsync();
        int splitFamilyCount = await _splitFamilyStore.CountAsync();

        return new RoadmapStateSummarySnapshot(
            activeArtifacts,
            lastDecision,
            splitFamilyCount,
            new ProjectionManifestCounts(
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Valid),
                manifest.Entries.Count(entry => entry.StaleStatus != ProjectionStaleStatus.Fresh),
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Invalid)));
    }

    private async Task<IReadOnlyList<ArtifactStateRow>> ActiveArtifactRowsAsync()
    {
        string[] paths =
        [
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.Selection,
            RoadmapArtifactPaths.ActiveEpic,
        ];
        var rows = new List<ArtifactStateRow>();
        foreach (string path in paths)
        {
            rows.Add(new ArtifactStateRow(Path.GetFileName(path), path, (await _artifacts.GetStatusAsync(path)).ToString()));
        }

        return rows;
    }

    private static IReadOnlyList<string> NextTransitions(RoadmapState state) =>
        state switch
        {
            RoadmapState.CoreReady => ["BootstrapRoadmapCompletionContext", "SelectNextStrategicInitiative"],
            RoadmapState.RoadmapCompletionContextReady => ["SelectNextStrategicInitiative"],
            RoadmapState.SelectNextStrategicInitiative => ["SelectNextEpic"],
            RoadmapState.ActiveEpicReady => ["GenerateMilestoneDeepDives"],
            RoadmapState.MilestoneSpecsReady => [],
            RoadmapState.EpicPreparationAudit => ["EpicPreparationAudit"],
            RoadmapState.RetireEpic => ["SelectNextStrategicInitiative"],
            RoadmapState.EvidenceGathering => ["GatherAdditionalEvidence", ExecutionCommandText(ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift)],
            RoadmapState.EvidenceBlocked => ["Resolve blocker and rerun"],
            _ => [],
        };

    private static string ExecutionCommandText(ExecutionDispositionCommand command) =>
        ExecutionDispositionProtocol.CommandText(command);

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "None" : string.Join(", ", values);

    private async Task<IReadOnlyList<string>> EnsureInvariantEvidencePathsAsync(
        InvariantValidationResult invariant,
        string transition,
        RoadmapState attemptedState,
        string reason,
        DateTimeOffset failedAt)
    {
        if (!string.IsNullOrWhiteSpace(invariant.EvidencePath))
        {
            return [invariant.EvidencePath];
        }

        string details = $"""
            Invariant validation reported a failure without returning an evidence path.

            | Field | Value |
            |---|---|
            | Attempted State | {attemptedState} |
            | Failure State | {invariant.FailureState} |
            | Invariant Category | {invariant.FailureCategory} |
            | Original Reason | {reason} |

            This fallback artifact exists only to keep workflow state recoverable when validator evidence is unavailable.
            """;
        string fallbackPath = await _artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "invariant-failure-missing-evidence",
            RoadmapBlockedArtifact.Render(
                invariant.FailureState,
                transition,
                "Invariant validation failed without validator evidence.",
                "Restore validator evidence or repair the invariant violation, then rerun the roadmap CLI.",
                "None",
                details,
                failedAt));
        return [fallbackPath];
    }
}
