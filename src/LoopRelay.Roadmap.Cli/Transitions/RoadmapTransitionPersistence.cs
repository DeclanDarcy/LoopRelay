namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapTransitionPersistence(
    RoadmapArtifacts artifacts,
    ProjectionManifestStore manifestStore,
    RoadmapStateStore stateStore,
    DecisionLedgerStore decisionLedger)
{
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
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        RoadmapStateSummarySnapshot summary = await CaptureSummaryAsync();
        IReadOnlyList<RetiredEpic> effectiveRetiredEpics = retiredEpics ?? existing?.RetiredEpics ?? [];
        IReadOnlyList<BlockerRow> effectiveBlockers = blockers ?? existing?.Blockers ?? [];

        await stateStore.SaveAsync(new RoadmapStateDocument(
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

    public async Task RefreshAndSaveAsync(RoadmapStateDocument document)
    {
        RoadmapStateSummarySnapshot summary = await CaptureSummaryAsync();
        await stateStore.SaveAsync(document with
        {
            ActiveArtifacts = summary.ActiveArtifacts,
            LastDecisionId = summary.LastDecisionId,
            RetiredEpicsCount = document.RetiredEpics.Count,
            SplitFamiliesCount = summary.SplitFamiliesCount,
            ProjectionManifestCounts = summary.ProjectionManifestCounts,
        });
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
        ProjectionManifest manifest = await manifestStore.LoadAsync();
        IReadOnlyList<ArtifactStateRow> activeArtifacts = await ActiveArtifactRowsAsync();
        string lastDecision = await decisionLedger.LastDecisionIdAsync();
        int splitFamilyCount = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json")).Count;

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
            rows.Add(new ArtifactStateRow(Path.GetFileName(path), path, (await artifacts.GetStatusAsync(path)).ToString()));
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
}

internal sealed record RoadmapStateSummarySnapshot(
    IReadOnlyList<ArtifactStateRow> ActiveArtifacts,
    string LastDecisionId,
    int SplitFamiliesCount,
    ProjectionManifestCounts ProjectionManifestCounts);
