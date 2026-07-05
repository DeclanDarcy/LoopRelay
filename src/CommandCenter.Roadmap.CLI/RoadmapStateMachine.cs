using System.Diagnostics;

namespace CommandCenter.Roadmap.Cli;

internal sealed class RoadmapStateMachine(
    RoadmapArtifacts artifacts,
    NorthStarContextLoader northStarLoader,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    RoadmapPromptRunner promptRunner,
    RoadmapStateStore stateStore,
    DecisionLedgerStore decisionLedger,
    TransitionJournalStore journalStore,
    ArtifactLifecycleStore lifecycleStore,
    BundleFileExtractor bundleExtractor,
    BundleManifestWriter bundleManifestWriter,
    SplitFamilyStore splitFamilyStore,
    OperationalContextGenerator operationalContextGenerator,
    ExecutionPromptGenerator executionPromptGenerator,
    ExecutionCompatibilityMaterializer executionMaterializer,
    IRoadmapExecutionBridge executionBridge,
    InvariantValidator invariantValidator,
    ILoopConsole console)
{
    public async Task<RoadmapOutcome> RunAsync(CancellationToken cancellationToken)
    {
        NorthStarContext northStar;
        try
        {
            console.Phase("Core preflight");
            northStar = await northStarLoader.LoadAsync(cancellationToken);
            await contractRegistry.EmitSnapshotAsync(artifacts);
            await SaveStateAsync(RoadmapState.CoreReady, TransitionStatus.Completed, RoadmapState.CoreReady, RoadmapState.CoreReady, "Preflight", "None", "None", "CoreReady", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
        }
        catch (RoadmapStepException exception)
        {
            await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "Preflight", exception.Message);
            console.Error(exception.Message);
            return RoadmapOutcome.PreflightBlocked;
        }

        try
        {
            if (!await artifacts.ExistsAsync(RoadmapArtifactPaths.RoadmapCompletionContext))
            {
                await BootstrapRoadmapCompletionContextAsync(northStar, cancellationToken);
            }

            SelectionDecision selection = await SelectNextInitiativeAsync(northStar, cancellationToken);
            switch (selection.RecommendedOutcome)
            {
                case "Select Existing Epic":
                    await AuditAndPrepareExistingEpicAsync(northStar, cancellationToken);
                    break;
                case "Select New Intermediary Epic":
                    await CreateNewEpicAsync(northStar, cancellationToken);
                    break;
                case "Select Split Epic":
                    await SplitEpicAsync(northStar, cancellationToken);
                    break;
                case "Strategic Investigation Required":
                    await AppendDecisionAsync(RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                    await SaveStateAsync(RoadmapState.StrategicInvestigationRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
                    return RoadmapOutcome.Paused;
                case "Roadmap Revision Required":
                    await AppendDecisionAsync(RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                    await SaveStateAsync(RoadmapState.RoadmapRevisionRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
                    return RoadmapOutcome.Paused;
                default:
                    await AppendDecisionAsync(RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                    await SaveStateAsync(RoadmapState.NoSuitableInitiative, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);
                    return RoadmapOutcome.Paused;
            }

            await GenerateMilestonesAndExecutionContextAsync(northStar, cancellationToken);
            await RunExecutionAndCertificationAsync(northStar, cancellationToken);
            return RoadmapOutcome.Completed;
        }
        catch (OperationCanceledException)
        {
            await SaveStateAsync(RoadmapState.Cancelled, TransitionStatus.Cancelled, RoadmapState.CoreReady, RoadmapState.Cancelled, "Cancellation", "None", "None", "Cancelled", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], [new BlockerRow("Cancelled", "Rerun the roadmap CLI when ready.")]);
            return RoadmapOutcome.Cancelled;
        }
        catch (RoadmapStepException exception)
        {
            await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message);
            console.Error(exception.Message);
            return RoadmapOutcome.Failed;
        }
    }

    private async Task BootstrapRoadmapCompletionContextAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateRoadmapCompletionContext";
        console.Phase("Bootstrap roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = "# Roadmap Completion Bootstrap\n\n## Projection Content\n\n" + projection.Content;
        string output = await RunPromptTransitionAsync(
            RoadmapState.CoreReady,
            RoadmapState.RoadmapCompletionContextReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.RoadmapCompletionContext, ArtifactLifecycleState.Ready);
    }

    private async Task<SelectionDecision> SelectNextInitiativeAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SelectNextEpic";
        console.Phase("Select next strategic initiative");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        string context = await contextBuilder.BuildSelectionContextAsync(projection.Content, existing?.RetiredEpicExclusions ?? []);
        string output = await RunPromptTransitionAsync(
            RoadmapState.RoadmapCompletionContextReady,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.Selection],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.Selection, output);
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "selection", output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.Selection, ArtifactLifecycleState.Ready, evidencePath);

        SelectionDecision decision = new SelectionParser().Parse(output);
        await AppendDecisionAsync(RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.Selection, decision.RecommendedOutcome, decision.Confidence, decision.PrimaryReason);
        return decision;
    }

    private async Task AuditAndPrepareExistingEpicAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "EpicPreparationAudit";
        console.Phase("Audit selected epic");
        string selection = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = contextBuilder.BuildAuditContext(projection.Content, selection);
        string output = await RunPromptTransitionAsync(
            RoadmapState.ExistingEpicSelected,
            RoadmapState.EpicPreparationAudit,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.AuditEvidenceDirectory],
            cancellationToken);
        string auditPath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "epic-preparation-audit", output);
        EpicPreparationAuditDecision decision = new EpicPreparationAuditParser().Parse(output);
        await AppendDecisionAsync(RoadmapState.EpicPreparationAudit, runtimePrompt, projection.Definition.ProjectionPath, auditPath, decision.Disposition, decision.Confidence, decision.RecommendedNextStep);

        if (decision.Disposition == "Retire")
        {
            RoadmapStateDocument? existing = await stateStore.LoadAsync();
            List<string> exclusions = (existing?.RetiredEpicExclusions ?? []).Append(decision.RecommendedNextStep).Distinct(StringComparer.Ordinal).ToList();
            await SaveStateAsync(RoadmapState.SelectNextStrategicInitiative, TransitionStatus.Completed, RoadmapState.RetireEpic, RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retire", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, exclusions, []);
            throw new RoadmapStepException("Selected epic was retired. Rerun selection with the retired exclusion recorded.");
        }

        if (decision.Disposition == "Insufficient Evidence")
        {
            throw new RoadmapStepException("Epic preparation audit requires more evidence.");
        }

        if (decision.Disposition == "Realign")
        {
            await RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, northStar, auditPath, cancellationToken);
        }
        else
        {
            await RewriteActiveEpicAsync("ReimagineEpic", RoadmapState.ReimagineEpic, northStar, auditPath, cancellationToken);
        }
    }

    private async Task RewriteActiveEpicAsync(string runtimePrompt, RoadmapState state, NorthStarContext northStar, string auditPath, CancellationToken cancellationToken)
    {
        console.Phase(runtimePrompt);
        string selectionOrEpic = await artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic) ?? await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        string audit = await artifacts.ReadRequiredAsync(auditPath);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit);
        string output = await RunPromptTransitionAsync(state, RoadmapState.ActiveEpicReady, runtimePrompt, projection.Definition.ProjectionPath, context, audit, [RoadmapArtifactPaths.ActiveEpic], cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.ActiveEpic, output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);
    }

    private async Task CreateNewEpicAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateNewEpic";
        console.Phase("Create new epic");
        string selection = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        string output = await RunPromptTransitionAsync(RoadmapState.NewEpicProposed, RoadmapState.ActiveEpicReady, runtimePrompt, projection.Definition.ProjectionPath, context, selection, [RoadmapArtifactPaths.ActiveEpic], cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.ActiveEpic, output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready);
    }

    private async Task SplitEpicAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SplitEpic";
        console.Phase("Split epic");
        string selection = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        string output = await RunPromptTransitionAsync(RoadmapState.SplitEpicProposed, RoadmapState.SplitChildSelection, runtimePrompt, projection.Definition.ProjectionPath, context, selection, [RoadmapArtifactPaths.SplitFamiliesDirectory], cancellationToken);
        BundleExtractionResult bundle = bundleExtractor.Extract(output);
        await bundleExtractor.WriteExtractedFilesAsync(artifacts, bundle);
        await bundleManifestWriter.WriteAsync(BundleManifestWriter.DefaultManifestPath(bundle.Files), runtimePrompt, projection.Definition.ProjectionPath, bundle, "Valid");

        string selectedChild = bundle.Files.OrderBy(file => file.Path, StringComparer.Ordinal).First().Path;
        var family = new SplitFamily(Guid.NewGuid().ToString("N")[..8], selection, bundle.Files.Select(file => file.Path).ToList(), bundle.Files.Select(file => file.Path).ToList(), selectedChild, "First child selected by deterministic MVP ordering.", DateTimeOffset.UtcNow);
        await splitFamilyStore.WriteAsync(family);
        InvariantValidationResult promotion = await invariantValidator.ValidateSplitChildPromotionAsync(selectedChild);
        if (!promotion.IsValid)
        {
            throw new RoadmapStepException(promotion.Error ?? "Split child promotion failed invariant validation.");
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.ActiveEpic, await artifacts.ReadRequiredAsync(selectedChild));
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Ready, selectedChild);
    }

    private async Task GenerateMilestonesAndExecutionContextAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "GenerateMilestoneDeepDivesForEpic";
        console.Phase("Generate milestone deep dives");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = await contextBuilder.BuildMilestoneContextAsync(projection.Content);
        string output = await RunPromptTransitionAsync(RoadmapState.ActiveEpicReady, RoadmapState.MilestoneSpecsReady, runtimePrompt, projection.Definition.ProjectionPath, context, string.Empty, [RoadmapArtifactPaths.SpecsDirectory], cancellationToken);
        BundleExtractionResult bundle = bundleExtractor.Extract(output);
        if (bundle.IsBlocked || bundle.Files.Count == 0)
        {
            throw new RoadmapStepException(bundle.BlockedReason ?? "Milestone deep dive output did not contain specs.");
        }

        await bundleExtractor.WriteExtractedFilesAsync(artifacts, bundle);
        await bundleManifestWriter.WriteAsync($"{RoadmapArtifactPaths.SpecsDirectory}/bundle-manifest.md", runtimePrompt, projection.Definition.ProjectionPath, bundle, "Valid");
        foreach (ExtractedBundleFile file in bundle.Files)
        {
            await lifecycleStore.UpsertAsync(file.Path, ArtifactLifecycleState.Ready);
        }

        InvariantValidationResult invariant = await invariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, northStar.Hash, cancellationToken);
        if (!invariant.IsValid)
        {
            throw new RoadmapStepException(invariant.Error ?? "Invariant validation failed after milestone generation.");
        }

        console.Phase("Generate operational context");
        await operationalContextGenerator.GenerateAsync(cancellationToken);
        console.Phase("Generate execution prompt");
        await executionPromptGenerator.GenerateAsync(cancellationToken);
        await executionMaterializer.MaterializeAsync(cancellationToken);
    }

    private async Task RunExecutionAndCertificationAsync(NorthStarContext northStar, CancellationToken cancellationToken)
    {
        InvariantValidationResult invariant = await invariantValidator.ValidateAsync(RoadmapState.ExecutionPromptReady, northStar.Hash, cancellationToken);
        if (!invariant.IsValid)
        {
            throw new RoadmapStepException(invariant.Error ?? "Invariant validation failed before execution.");
        }

        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Executing);
        RoadmapExecutionBridgeResult bridge = await executionBridge.RunAsync(cancellationToken);
        if (!bridge.EpicCompleted)
        {
            throw new RoadmapStepException(bridge.Message);
        }

        const string runtimePrompt = "EvaluateEpicCompletionAndDrift";
        console.Phase("Evaluate epic completion and drift");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionEvaluationContextAsync(projection.Content);
        string output = await RunPromptTransitionAsync(RoadmapState.EpicCompletionDetected, RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, context, string.Empty, [RoadmapArtifactPaths.EvaluationEvidenceDirectory], cancellationToken);
        string evaluationPath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "epic-completion-and-drift", output);
        CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(output);
        await AppendDecisionAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, evaluationPath, decision.ClosureRecommendation, "Unclear", decision.OverallCompletionStatus);

        if (decision.ClosureRecommendation is not ("Close Epic" or "Close With Follow-Up"))
        {
            throw new RoadmapStepException($"Completion certification routed to {decision.ClosureRecommendation}.");
        }

        await UpdateRoadmapCompletionContextAsync(northStar, evaluationPath, cancellationToken);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Completed);
    }

    private async Task UpdateRoadmapCompletionContextAsync(NorthStarContext northStar, string evaluationPath, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "UpdateRoadmapCompletionContext";
        console.Phase("Update roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, northStar, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionUpdateContextAsync(projection.Content, evaluationPath);
        string output = await RunPromptTransitionAsync(RoadmapState.CompletionEvaluationAndContextUpdate, RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, context, string.Empty, [RoadmapArtifactPaths.RoadmapCompletionContext], cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "roadmap-completion-update", output);
        await AppendDecisionAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.RoadmapCompletionContext, "Roadmap Completion Context Updated", "Unclear", "Completion context updated after certification.");
    }

    private async Task<string> RunPromptTransitionAsync(
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken)
    {
        string correlationId = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        await SaveStateAsync(to, TransitionStatus.Started, from, to, prompt, projectionPath, string.Join(", ", outputs), "Pending", started, null, [], []);
        await journalStore.AppendAsync(new TransitionJournalRecord("TransitionStarted", correlationId, started, from, to, prompt, projectionPath, prompt, await HashInputsAsync([]), outputs, 0, "Started", "None", null));

        try
        {
            string output = await promptRunner.RunRuntimePromptAsync(prompt, projectContext, secondaryInput, cancellationToken);
            stopwatch.Stop();
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("TransitionCompleted", correlationId, completed, from, to, prompt, projectionPath, prompt, await HashInputsAsync([]), outputs, stopwatch.ElapsedMilliseconds, "Completed", "None", null));
            await SaveStateAsync(to, TransitionStatus.Completed, from, to, prompt, projectionPath, string.Join(", ", outputs), "Completed", started, completed, [], []);
            return output;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            DateTimeOffset failed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("TransitionFailed", correlationId, failed, from, to, prompt, projectionPath, prompt, await HashInputsAsync([]), outputs, stopwatch.ElapsedMilliseconds, "Failed", "None", exception.Message));
            await SaveStateAsync(RoadmapState.EvidenceBlocked, TransitionStatus.Failed, from, to, prompt, projectionPath, string.Join(", ", outputs), "Failed", started, failed, [], [new BlockerRow(exception.Message, "Review the transition failure and rerun.")]);
            throw;
        }
    }

    private async Task AppendDecisionAsync(RoadmapState state, string transition, string projectionPath, string outputPath, string decision, string confidence, string rationale)
    {
        string id = await decisionLedger.NextDecisionIdAsync();
        await decisionLedger.AppendAsync(new DecisionLedgerEntry(
            id,
            DateTimeOffset.UtcNow,
            state,
            transition,
            transition,
            projectionPath,
            [],
            [outputPath],
            decision,
            confidence,
            rationale));
    }

    private async Task SaveStateAsync(
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
        IReadOnlyList<string> retiredExclusions,
        IReadOnlyList<BlockerRow> blockers)
    {
        ProjectionManifest manifest = await manifestStore.LoadAsync();
        IReadOnlyList<ArtifactStateRow> activeArtifacts = await ActiveArtifactRowsAsync();
        string lastDecision = await decisionLedger.LastDecisionIdAsync();
        int splitFamilyCount = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md")).Count;
        await stateStore.SaveAsync(new RoadmapStateDocument(
            current,
            activeArtifacts,
            new RoadmapTransitionSummary(from, to, prompt, projection, output, decision, status, started, completed),
            blockers,
            lastDecision,
            retiredExclusions.Count,
            splitFamilyCount,
            new ProjectionManifestCounts(
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Valid),
                manifest.Entries.Count(entry => entry.StaleStatus == ProjectionStaleStatus.Stale),
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Invalid)),
            NextTransitions(current),
            retiredExclusions));
    }

    private async Task<IReadOnlyList<ArtifactStateRow>> ActiveArtifactRowsAsync()
    {
        string[] paths =
        [
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.Selection,
            RoadmapArtifactPaths.ActiveEpic,
            RoadmapArtifactPaths.OperationalContext,
            RoadmapArtifactPaths.ExecutionPrompt,
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
            RoadmapState.ActiveEpicReady => ["GenerateMilestoneDeepDives"],
            RoadmapState.MilestoneSpecsReady => ["GenerateOperationalContext"],
            RoadmapState.ExecutionPromptReady => ["ExecutionLoop"],
            RoadmapState.EvidenceBlocked => ["Resolve blocker and rerun"],
            _ => [],
        };

    private async Task<IReadOnlyDictionary<string, string>> HashInputsAsync(IReadOnlyList<string> inputs)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string input in inputs)
        {
            string? content = await artifacts.ReadAsync(input);
            if (content is not null)
            {
                hashes[input] = RoadmapHash.Sha256(content);
            }
        }

        return hashes;
    }

    private async Task WriteBlockedStateAsync(RoadmapState state, string transition, string reason)
    {
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "roadmap-transition-blocked",
            RoadmapBlockedArtifact.Render(state, transition, reason, "Address the blocker and rerun the roadmap CLI.", "None", reason, DateTimeOffset.UtcNow));
        await SaveStateAsync(state, TransitionStatus.Failed, RoadmapState.CoreReady, state, transition, "None", path, "Blocked", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], [new BlockerRow(reason, "Address the blocker and rerun the roadmap CLI.")]);
    }
}
