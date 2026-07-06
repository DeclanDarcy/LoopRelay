using System.Diagnostics;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapStateMachine(
    RoadmapArtifacts artifacts,
    ProjectContextLoader projectContextLoader,
    PromptContractRegistry contractRegistry,
    ProjectionManifestStore manifestStore,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    TransitionInputResolver inputResolver,
    CompletionCertificationPolicy completionPolicy,
    CompletionCertificationRouter completionRouter,
    RoadmapPromptRunner promptRunner,
    RoadmapStateStore stateStore,
    RoadmapStartupPlanner startupPlanner,
    RoadmapResumePlanner resumePlanner,
    RoadmapUnblockPlanner unblockPlanner,
    SelectionProvenanceService selectionProvenance,
    DecisionLedgerStore decisionLedger,
    TransitionJournalStore journalStore,
    ArtifactLifecycleStore lifecycleStore,
    ArtifactPromotionService promotionService,
    BundleFileExtractor bundleExtractor,
    SplitEpicBundleInterpreter splitBundleInterpreter,
    BundleManifestWriter bundleManifestWriter,
    SplitFamilyStore splitFamilyStore,
    ExecutionPreparationProvenanceService executionPreparation,
    OperationalContextGenerator operationalContextGenerator,
    ExecutionPromptGenerator executionPromptGenerator,
    ExecutionCompatibilityMaterializer executionMaterializer,
    IRoadmapExecutionBridge executionBridge,
    RoadmapExecutionOutcomeInterpreter executionInterpreter,
    InvariantValidator invariantValidator,
    ILoopConsole console)
{
    public Task<RoadmapOutcome> ExecuteAsync(
        RoadmapCliCommand command,
        CancellationToken cancellationToken) =>
        command switch
        {
            RoadmapCliCommand.Status => StatusAsync(cancellationToken),
            RoadmapCliCommand.Run => RunAsync(cancellationToken),
            RoadmapCliCommand.Unblock => UnblockAsync(cancellationToken),
            _ => throw new RoadmapStepException($"Unsupported roadmap command: {command}."),
        };

    public async Task<RoadmapOutcome> StatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RoadmapStateDocument? persistedState = await stateStore.LoadAsync();
        if (persistedState is null)
        {
            console.Info("No persisted roadmap state exists.");
            return RoadmapOutcome.Paused;
        }

        RoadmapStartupPlan startupPlan = startupPlanner.Plan(persistedState);
        console.Info($"Status: {persistedState.CurrentState}. {startupPlan.Reason}");
        console.Info($"Transition intent: {persistedState.TransitionIntent.Intent} -> {persistedState.TransitionIntent.DispatchState}");
        foreach (BlockerRow blocker in persistedState.Blockers)
        {
            console.Warn($"{blocker.Blocker} Required next step: {blocker.RequiredNextStep}");
        }

        return startupPlan.ReportOutcome ?? RoadmapOutcome.Paused;
    }

    public async Task<RoadmapOutcome> UnblockAsync(CancellationToken cancellationToken)
    {
        RoadmapStateDocument? persistedState = await stateStore.LoadAsync();
        RoadmapUnblockPlan unblockPlan = await unblockPlanner.PlanAsync(persistedState, cancellationToken);
        console.Info($"Unblock plan: {unblockPlan.Status} for {unblockPlan.TransitionIntent.Intent}. {unblockPlan.Reason}");

        if (persistedState is null)
        {
            return RoadmapOutcome.Paused;
        }

        if (unblockPlan.Status != RoadmapUnblockPlanStatus.Success)
        {
            if (IsBlockedRecoveryState(persistedState.CurrentState))
            {
                await PersistUnblockReviewFailureAsync(persistedState, unblockPlan);
            }

            return RoadmapWorkflowStateClassifier.ReportOutcome(persistedState.CurrentState);
        }

        return unblockPlan.Action switch
        {
            RoadmapUnblockAction.RecoverToCoreReady => await RecoverPreflightBlockerAsync(persistedState, unblockPlan),
            RoadmapUnblockAction.RecoverExecutionDisposition => await RecoverExecutionDispositionAsync(persistedState, unblockPlan),
            RoadmapUnblockAction.RecoverCompletionCertification => await RecoverCompletionCertificationAsync(persistedState, unblockPlan, cancellationToken),
            RoadmapUnblockAction.RecoverExecutionRuntimeFailure => await RecoverExecutionRuntimeFailureAsync(persistedState, unblockPlan),
            _ => throw new RoadmapStepException($"Unsupported unblock recovery action: {unblockPlan.Action}."),
        };
    }

    public async Task<RoadmapOutcome> RunAsync(CancellationToken cancellationToken)
    {
        RoadmapStateDocument? persistedState = await stateStore.LoadAsync();
        RoadmapStartupPlan startupPlan = startupPlanner.Plan(persistedState);
        console.Info($"Startup plan: {startupPlan.Action} from {startupPlan.SourceState}. {startupPlan.Reason}");

        if (startupPlan.PreflightRequirement == RoadmapPreflightRequirement.None)
        {
            return startupPlan.ReportOutcome ?? RoadmapOutcome.Paused;
        }

        ProjectContext projectContext;
        try
        {
            console.Phase("Project Context preflight");
            projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            await contractRegistry.EmitSnapshotAsync(artifacts);
        }
        catch (RoadmapStepException exception)
        {
            if (startupPlan.PreflightRequirement == RoadmapPreflightRequirement.RequiredForInitialize || persistedState is null)
            {
                await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "Preflight", exception.Message);
            }
            else
            {
                await WritePreflightInterruptedStateAsync(persistedState, exception.Message);
            }

            console.Error(exception.Message);
            return RoadmapOutcome.PreflightBlocked;
        }

        try
        {
            RoadmapResumePlan resumePlan = await resumePlanner.PlanAsync(persistedState, projectContext, cancellationToken);
            console.Info($"Resume plan: {resumePlan.Action} from {resumePlan.SourceState}. {resumePlan.Reason}");
            return await ExecuteResumePlanAsync(resumePlan, projectContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await WriteCancelledStateAsync();
            return RoadmapOutcome.Cancelled;
        }
        catch (RoadmapStepException exception) when (exception.Persistence == RoadmapFailurePersistence.AlreadyPersisted)
        {
            console.Error(exception.Message);
            return RoadmapOutcome.Failed;
        }
        catch (RoadmapStepException exception)
        {
            await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message);
            console.Error(exception.Message);
            return RoadmapOutcome.Failed;
        }
        catch (Exception exception)
        {
            await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "RoadmapStateMachine", exception.Message);
            console.Error(exception.Message);
            return RoadmapOutcome.Failed;
        }
    }

    private async Task<RoadmapOutcome> ExecuteResumePlanAsync(
        RoadmapResumePlan resumePlan,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        switch (resumePlan.Action)
        {
            case RoadmapResumeAction.ContinueFromCoreReady:
                if (resumePlan.ShouldPersistCoreReady)
                {
                    DateTimeOffset completed = DateTimeOffset.UtcNow;
                    await SaveStateAsync(
                        RoadmapState.CoreReady,
                        TransitionStatus.Completed,
                        RoadmapState.CoreReady,
                        RoadmapState.CoreReady,
                        "Preflight",
                        "None",
                        "None",
                        "CoreReady",
                        completed,
                        completed,
                        null,
                        null);
                }

                return await RunFromCoreReadyAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.SelectNextStrategicInitiative:
                return await RunSelectionAndFollowingAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.ContinueSelectionDecision:
            {
                string selectionOutput = await ReadCurrentSelectionAsync(cancellationToken);
                SelectionDecision selection = new SelectionParser().Parse(selectionOutput);
                return await ContinueAfterSelectionAsync(selection, projectContext, cancellationToken);
            }

            case RoadmapResumeAction.PrepareExecutionFromActiveEpic:
                await GenerateMilestoneSpecsAsync(projectContext, cancellationToken);
                await EnsureExecutionReadinessAsync(RoadmapState.MilestoneSpecsReady, cancellationToken);
                return await RunExecutionAndCertificationAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.PrepareExecutionFromMilestoneSpecs:
                await EnsureExecutionReadinessAsync(RoadmapState.MilestoneSpecsReady, cancellationToken);
                return await RunExecutionAndCertificationAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.PrepareExecutionFromOperationalContext:
                await EnsureExecutionReadinessAsync(RoadmapState.OperationalContextReady, cancellationToken);
                return await RunExecutionAndCertificationAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.RunExecution:
                await executionMaterializer.MaterializeAsync(cancellationToken);
                return await RunExecutionAndCertificationAsync(projectContext, cancellationToken);

            case RoadmapResumeAction.EvaluateCompletionClaim:
                return await RunCompletionCertificationAsync(
                    projectContext,
                    DateTimeOffset.UtcNow,
                    await ReadPersistedExecutionEvidencePathAsync(),
                    cancellationToken,
                    persistCompletionClaim: false);

            case RoadmapResumeAction.Terminal:
                return resumePlan.TerminalOutcome ?? RoadmapOutcome.Paused;

            case RoadmapResumeAction.Block:
                console.Warn(resumePlan.Reason);
                await WriteBlockedStateAsync(RoadmapState.EvidenceBlocked, "ResumePlanning", resumePlan.Reason);
                return RoadmapOutcome.Paused;

            default:
                throw new RoadmapStepException($"Unsupported resume action: {resumePlan.Action}.");
        }
    }

    private async Task<RoadmapOutcome> RecoverPreflightBlockerAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan)
    {
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: true);
        await RecordUnblockJournalAsync(persistedState, unblockPlan, reviewPath, "Recovered", null);

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        await SaveStateAsync(
            RoadmapState.CoreReady,
            TransitionStatus.Completed,
            persistedState.CurrentState,
            RoadmapState.CoreReady,
            "UnblockReview",
            "None",
            FormatList([..unblockPlan.TransitionIntent.EvidencePaths, reviewPath]),
            unblockPlan.Decision,
            completed,
            completed,
            null,
            [],
            RoadmapTransitionIntent.Empty(RoadmapState.CoreReady));
        return RoadmapOutcome.Paused;
    }

    private async Task<RoadmapOutcome> RecoverExecutionDispositionAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan)
    {
        ExecutionDispositionValidationResult validation = unblockPlan.ExecutionValidation
            ?? throw new RoadmapStepException("Execution disposition unblock did not include validation evidence.");
        ExecutionDispositionRoute route = validation.Route
            ?? throw new RoadmapStepException("Execution disposition unblock did not include a validated route.");
        string executionEvidencePath = unblockPlan.PrimaryEvidencePath
            ?? throw new RoadmapStepException("Execution disposition unblock did not include an execution evidence path.");
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: true);
        await RecordUnblockJournalAsync(persistedState, unblockPlan, reviewPath, "Recovered", null);

        if (route.OutcomeKind is RoadmapExecutionOutcomeKind.ContinueRequired or RoadmapExecutionOutcomeKind.ExecutionBlocked)
        {
            await lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                ArtifactLifecycleState.Executing,
                $"Unblock review routed execution disposition: {validation.Disposition.StatusText}.");
        }

        IReadOnlyList<string> outputs = [executionEvidencePath, reviewPath];
        IReadOnlyList<BlockerRow> blockers = route.OutcomeKind == RoadmapExecutionOutcomeKind.ExecutionBlocked
            ? [new BlockerRow(validation.Disposition.EvidenceSummary, $"Review {executionEvidencePath}, resolve the execution blocker, and run explicit unblock when a handler exists.")]
            : [];
        TransitionStatus status = route.OutcomeKind == RoadmapExecutionOutcomeKind.EpicComplete
            ? TransitionStatus.Completed
            : TransitionStatus.Paused;
        IReadOnlyList<string> nextTransitions = route.OutcomeKind switch
        {
            RoadmapExecutionOutcomeKind.EpicComplete => [route.WorkflowTransition],
            RoadmapExecutionOutcomeKind.ContinueRequired => ["ExecutionLoop", route.WorkflowTransition],
            RoadmapExecutionOutcomeKind.ExecutionBlocked => [route.WorkflowTransition],
            _ => [route.WorkflowTransition],
        };

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        await SaveStateAsync(
            route.TargetState,
            status,
            persistedState.CurrentState,
            route.TargetState,
            "UnblockReview",
            "None",
            FormatList(outputs),
            validation.Disposition.StatusText,
            completed,
            completed,
            null,
            blockers,
            new RoadmapTransitionIntent(route.WorkflowTransition, route.TargetState, outputs),
            nextTransitions);
        return RoadmapOutcome.Paused;
    }

    private async Task<RoadmapOutcome> RecoverCompletionCertificationAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan,
        CancellationToken cancellationToken)
    {
        CompletionCertificationPolicyResult certification = unblockPlan.CompletionCertification
            ?? throw new RoadmapStepException("Completion certification unblock did not include certification evidence.");
        CompletionCertificationRoute route = unblockPlan.CompletionRoute
            ?? throw new RoadmapStepException("Completion certification unblock did not include a route.");
        string evaluationPath = unblockPlan.PrimaryEvidencePath
            ?? throw new RoadmapStepException("Completion certification unblock did not include an evaluation evidence path.");
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: true);
        await RecordUnblockJournalAsync(persistedState, unblockPlan, reviewPath, "Recovered", null);
        await AppendDecisionAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            "UnblockReview",
            persistedState.LastTransition.Projection,
            evaluationPath,
            certification.Decision.ClosureRecommendation,
            "Unclear",
            unblockPlan.Reason);

        if (route.RequiresRoadmapCompletionContextUpdate)
        {
            ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            await UpdateRoadmapCompletionContextAsync(projectContext, evaluationPath, cancellationToken);
        }

        if (route.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                activeEpicLifecycleState,
                $"Completion certification unblock route: {route.ClosureRecommendation}");
        }

        await PersistCompletionRouteAsync(
            route,
            certification.Decision,
            persistedState.LastTransition.Projection,
            evaluationPath,
            [reviewPath],
            []);
        return route.CliOutcome;
    }

    private async Task<RoadmapOutcome> RecoverExecutionRuntimeFailureAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan)
    {
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: true);
        await RecordUnblockJournalAsync(persistedState, unblockPlan, reviewPath, "Recovered", null);
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.ActiveEpic,
            ArtifactLifecycleState.Executing,
            "Execution runtime failure was unblocked for a safe retry.");

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        IReadOnlyList<string> outputs = [..unblockPlan.TransitionIntent.EvidencePaths, reviewPath];
        await SaveStateAsync(
            RoadmapState.ExecutionPromptReady,
            TransitionStatus.Completed,
            persistedState.CurrentState,
            RoadmapState.ExecutionPromptReady,
            "UnblockReview",
            "None",
            FormatList(outputs),
            unblockPlan.Decision,
            completed,
            completed,
            null,
            [],
            new RoadmapTransitionIntent(
                ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution),
                RoadmapState.ExecutionPromptReady,
                [RoadmapArtifactPaths.ExecutionPrompt, reviewPath]),
            ["ExecutionLoop", ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution)]);
        return RoadmapOutcome.Paused;
    }

    private async Task PersistUnblockReviewFailureAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan)
    {
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: false);
        await RecordUnblockJournalAsync(
            persistedState,
            unblockPlan,
            reviewPath,
            unblockPlan.Status.ToString(),
            unblockPlan.Reason);

        ProjectionManifest manifest = await manifestStore.LoadAsync();
        int splitFamilyCount = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md")).Count;
        await stateStore.SaveAsync(persistedState with
        {
            ActiveArtifacts = await ActiveArtifactRowsAsync(),
            Blockers = AppendUnblockReviewBlocker(
                persistedState.Blockers,
                unblockPlan,
                reviewPath),
            LastDecisionId = await decisionLedger.LastDecisionIdAsync(),
            RetiredEpicsCount = persistedState.RetiredEpics.Count,
            SplitFamiliesCount = splitFamilyCount,
            ProjectionManifestCounts = new ProjectionManifestCounts(
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Valid),
                manifest.Entries.Count(entry => entry.StaleStatus != ProjectionStaleStatus.Fresh),
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Invalid)),
            NextValidTransitions = AppendNextTransition(persistedState.NextValidTransitions, "unblock"),
        });
    }

    private async Task<string> WriteUnblockReviewEvidenceAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan,
        bool success)
    {
        string content = RenderUnblockReviewEvidence(persistedState, unblockPlan, success, DateTimeOffset.UtcNow);
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "unblock-review",
            content);
        await lifecycleStore.UpsertAsync(
            path,
            success ? ArtifactLifecycleState.Ready : ArtifactLifecycleState.Blocked,
            unblockPlan.Reason);
        return path;
    }

    private async Task RecordUnblockJournalAsync(
        RoadmapStateDocument persistedState,
        RoadmapUnblockPlan unblockPlan,
        string reviewPath,
        string result,
        string? errorMessage)
    {
        IReadOnlyDictionary<string, string> hashes = new SortedDictionary<string, string>(
            unblockPlan.Evidence
                .Where(evidence => !string.IsNullOrWhiteSpace(evidence.Hash))
                .GroupBy(evidence => evidence.Path, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Last().Hash,
                    StringComparer.Ordinal),
            StringComparer.Ordinal);
        IReadOnlyList<string> outputs =
        [
            ..unblockPlan.Evidence
                .Where(evidence => evidence.Path.StartsWith(".agents/", StringComparison.Ordinal))
                .Select(evidence => evidence.Path)
                .Distinct(StringComparer.Ordinal),
            reviewPath,
        ];
        await journalStore.AppendAsync(new TransitionJournalRecord(
            unblockPlan.Status == RoadmapUnblockPlanStatus.Success ? "UnblockReviewCompleted" : "UnblockReviewBlocked",
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            persistedState.CurrentState,
            unblockPlan.TargetState ?? persistedState.CurrentState,
            "UnblockReview",
            "None",
            unblockPlan.TransitionIntent.Intent,
            hashes,
            outputs,
            0,
            result,
            unblockPlan.Decision,
            errorMessage,
            null));
    }

    private async Task<RoadmapOutcome> RunFromCoreReadyAsync(
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        if (await artifacts.GetStatusAsync(RoadmapArtifactPaths.RoadmapCompletionContext) != ArtifactStatus.Present)
        {
            await BootstrapRoadmapCompletionContextAsync(projectContext, cancellationToken);
        }

        return await RunSelectionAndFollowingAsync(projectContext, cancellationToken);
    }

    private async Task<RoadmapOutcome> RunSelectionAndFollowingAsync(
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        SelectionDecision selection = await SelectNextInitiativeAsync(projectContext, cancellationToken);
        return await ContinueAfterSelectionAsync(selection, projectContext, cancellationToken);
    }

    private async Task<RoadmapOutcome> ContinueAfterSelectionAsync(
        SelectionDecision selection,
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        switch (selection.RecommendedOutcome)
        {
            case "Select Existing Epic":
                EpicPreparationResult preparation = await AuditAndPrepareExistingEpicAsync(selection, projectContext, cancellationToken);
                if (preparation == EpicPreparationResult.Retired)
                {
                    return RoadmapOutcome.Paused;
                }

                if (preparation == EpicPreparationResult.Blocked)
                {
                    return RoadmapOutcome.Paused;
                }

                break;
            case "Select New Intermediary Epic":
                ArtifactPromotionResult createPromotion = await CreateNewEpicAsync(projectContext, cancellationToken);
                if (!createPromotion.Promoted)
                {
                    return RoadmapOutcome.Paused;
                }

                break;
            case "Select Split Epic":
                ArtifactPromotionResult splitPromotion = await SplitEpicAsync(projectContext, cancellationToken);
                if (!splitPromotion.Promoted)
                {
                    return RoadmapOutcome.Paused;
                }

                break;
            case "Strategic Investigation Required":
                await AppendDecisionAsync(RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.StrategicInvestigationRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
            case "Roadmap Revision Required":
                await AppendDecisionAsync(RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.RoadmapRevisionRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
            default:
                await AppendDecisionAsync(RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.NoSuitableInitiative, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
        }

        await GenerateMilestoneSpecsAsync(projectContext, cancellationToken);
        await EnsureExecutionReadinessAsync(RoadmapState.MilestoneSpecsReady, cancellationToken);
        return await RunExecutionAndCertificationAsync(projectContext, cancellationToken);
    }

    private async Task BootstrapRoadmapCompletionContextAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateRoadmapCompletionContext";
        console.Phase("Bootstrap roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = "# Roadmap Completion Bootstrap\n\n## Projection Content\n\n" + projection.Content;
        string completedEpicEvidence = await new CompletedEpicEvidenceLoader(artifacts).RenderAsync();
        string output = await RunPromptTransitionAsync(
            RoadmapState.CoreReady,
            RoadmapState.RoadmapCompletionContextReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            completedEpicEvidence,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.RoadmapCompletionContext, ArtifactLifecycleState.Ready);
    }

    private async Task<SelectionDecision> SelectNextInitiativeAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SelectNextEpic";
        console.Phase("Select next strategic initiative");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        string context = await contextBuilder.BuildSelectionContextAsync(projection.Content, existing?.RetiredEpics ?? []);
        PromptTransitionCompletion completion = await RunPromptTransitionWithCompletionAsync(
            RoadmapState.RoadmapCompletionContextReady,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.Selection],
            cancellationToken);
        await artifacts.WriteAsync(RoadmapArtifactPaths.Selection, completion.Output);
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "selection", completion.Output);
        await selectionProvenance.RecordActiveSelectionAsync(
            completion.Output,
            completion.InputSnapshot,
            existing?.RetiredEpics ?? [],
            cancellationToken);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.Selection, ArtifactLifecycleState.Ready, evidencePath);

        SelectionDecision decision = new SelectionParser().Parse(completion.Output);
        await AppendDecisionAsync(RoadmapState.SelectNextStrategicInitiative, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.Selection, decision.RecommendedOutcome, decision.Confidence, decision.PrimaryReason);
        return decision;
    }

    private async Task<EpicPreparationResult> AuditAndPrepareExistingEpicAsync(SelectionDecision selectionDecision, ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "EpicPreparationAudit";
        console.Phase("Audit selected epic");
        string selection = await ReadCurrentSelectionAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
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
            DateTimeOffset retiredAt = DateTimeOffset.UtcNow;
            RetiredEpic retired = RetiredEpic.FromSelectionAndAudit(selectionDecision, decision, auditPath, retiredAt);
            IReadOnlyList<RetiredEpic> retiredEpics = RetiredEpic.Upsert(existing?.RetiredEpics ?? [], retired);
            await AppendDecisionAsync(RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", decision.Confidence, $"{retired.IdentityKind} {retired.StableIdentity}: {decision.PrimaryReason}");
            await SaveStateAsync(RoadmapState.RetireEpic, TransitionStatus.Completed, RoadmapState.EpicPreparationAudit, RoadmapState.RetireEpic, runtimePrompt, projection.Definition.ProjectionPath, auditPath, "Retired Epic", retiredAt, retiredAt, retiredEpics, null);
            await SupersedeActiveSelectionAsync(
                [DerivedArtifactStaleReason.RetiredEpicStateDrift],
                "Retired epic state changed after EpicPreparationAudit.");
            return EpicPreparationResult.Retired;
        }

        if (decision.Disposition == "Insufficient Evidence")
        {
            throw new RoadmapStepException("Epic preparation audit requires more evidence.");
        }

        if (decision.Disposition == "Realign")
        {
            ArtifactPromotionResult promotion = await RewriteActiveEpicAsync("RealignEpic", RoadmapState.RealignEpic, projectContext, auditPath, cancellationToken);
            return promotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
        }

        ArtifactPromotionResult reimaginePromotion = await RewriteActiveEpicAsync("ReimagineEpic", RoadmapState.ReimagineEpic, projectContext, auditPath, cancellationToken);
        return reimaginePromotion.Promoted ? EpicPreparationResult.ActiveEpicReady : EpicPreparationResult.Blocked;
    }

    private async Task<ArtifactPromotionResult> RewriteActiveEpicAsync(string runtimePrompt, RoadmapState state, ProjectContext projectContext, string auditPath, CancellationToken cancellationToken)
    {
        console.Phase(runtimePrompt);
        string selectionOrEpic = await artifacts.ReadAsync(RoadmapArtifactPaths.ActiveEpic) ?? await ReadCurrentSelectionAsync(cancellationToken);
        string audit = await artifacts.ReadRequiredAsync(auditPath);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildRealignOrReimagineContext(projection.Content, selectionOrEpic, audit);
        PromptTransitionCompletion completion = await RunPromptForPromotionAsync(
            state,
            RoadmapState.ActiveEpicReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            audit,
            [RoadmapArtifactPaths.ActiveEpic],
            cancellationToken,
            TransitionInputContext.AuditEvidence(auditPath));
        return await PromoteActiveEpicAsync(state, runtimePrompt, projection.Definition.ProjectionPath, completion);
    }

    private async Task<ArtifactPromotionResult> CreateNewEpicAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "CreateNewEpic";
        console.Phase("Create new epic");
        string selection = await ReadCurrentSelectionAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        PromptTransitionCompletion completion = await RunPromptForPromotionAsync(RoadmapState.NewEpicProposed, RoadmapState.ActiveEpicReady, runtimePrompt, projection.Definition.ProjectionPath, context, selection, [RoadmapArtifactPaths.ActiveEpic], cancellationToken);
        return await PromoteActiveEpicAsync(RoadmapState.NewEpicProposed, runtimePrompt, projection.Definition.ProjectionPath, completion);
    }

    private async Task<ArtifactPromotionResult> SplitEpicAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SplitEpic";
        console.Phase("Split epic");
        string selection = await ReadCurrentSelectionAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        PromptTransitionCompletion completion = await RunPromptTransitionWithCompletionAsync(
            RoadmapState.SplitEpicProposed,
            RoadmapState.SplitChildSelection,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            selection,
            [RoadmapArtifactPaths.SplitFamiliesDirectory],
            cancellationToken);

        BundleExtractionResult bundle;
        try
        {
            bundle = bundleExtractor.Extract(completion.Output, BundleExtractionPolicy.RepositorySafe);
        }
        catch (RoadmapStepException exception)
        {
            SplitEpicBundleInterpretation extractionFailure = SplitEpicBundleInterpretation.Invalid(
                exception.Message,
                [new SplitEpicBundleRejection("SplitEpic output", exception.Message)]);
            return await BlockSplitEpicAsync(runtimePrompt, projection.Definition.ProjectionPath, completion, extractionFailure);
        }

        SplitEpicBundleInterpretation interpretation = splitBundleInterpreter.Interpret(bundle, completion.Output);
        if (!interpretation.IsValid)
        {
            return await BlockSplitEpicAsync(runtimePrompt, projection.Definition.ProjectionPath, completion, interpretation);
        }

        BundleExtractionResult validatedBundle = BundleExtractionResult.Extracted(interpretation.ValidatedChildEpics);
        await bundleExtractor.WriteExtractedFilesAsync(artifacts, validatedBundle);
        await bundleManifestWriter.WriteAsync(BundleManifestWriter.DefaultManifestPath(interpretation.ValidatedChildEpics), runtimePrompt, projection.Definition.ProjectionPath, validatedBundle, "Valid");
        foreach (ExtractedBundleFile child in interpretation.ValidatedChildEpics)
        {
            await lifecycleStore.UpsertAsync(child.Path, ArtifactLifecycleState.Draft, "Validated split child epic.");
        }

        ExtractedBundleFile selectedChild = interpretation.SelectedChild
            ?? throw new RoadmapStepException("Validated SplitEpic bundle did not select a child epic.");
        var family = new SplitFamily(
            Guid.NewGuid().ToString("N")[..8],
            selection,
            interpretation.ValidatedChildEpics.Select(file => file.Path).ToList(),
            interpretation.ValidatedChildEpics.Select(file => file.Path).ToList(),
            selectedChild.Path,
            interpretation.SelectedChildRationale,
            DateTimeOffset.UtcNow);
        await splitFamilyStore.WriteAsync(family);

        PromptTransitionCompletion childPromotionCompletion = completion with { Output = selectedChild.Content };
        return await PromoteActiveEpicAsync(
            RoadmapState.SplitChildSelection,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            childPromotionCompletion,
            $"Promoted split child {selectedChild.Path} by {runtimePrompt}.");
    }

    private async Task<ArtifactPromotionResult> BlockSplitEpicAsync(
        string runtimePrompt,
        string projectionPath,
        PromptTransitionCompletion completion,
        SplitEpicBundleInterpretation interpretation)
    {
        string reason = DescribeSplitInterpretation(interpretation);
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "split-epic-output",
            RenderSplitInterpretationEvidence(interpretation, completion.Output));
        await lifecycleStore.UpsertAsync(evidencePath, ArtifactLifecycleState.Blocked, reason);

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        string decision = interpretation.Status == SplitEpicBundleInterpretationStatus.Blocked
            ? "Split Epic Blocked"
            : "Split Bundle Rejected";
        ArtifactPromotionStatus status = interpretation.Status == SplitEpicBundleInterpretationStatus.Blocked
            ? ArtifactPromotionStatus.Blocked
            : ArtifactPromotionStatus.StructurallyInvalid;

        await journalStore.AppendAsync(new TransitionJournalRecord(
            "SplitBundleRejected",
            completion.CorrelationId,
            completed,
            RoadmapState.SplitChildSelection,
            RoadmapState.EvidenceBlocked,
            runtimePrompt,
            projectionPath,
            "SplitEpicBundleInterpreter",
            completion.InputSnapshot.ToInputArtifactHashes(),
            [evidencePath],
            completion.ElapsedMilliseconds,
            status.ToString(),
            decision,
            reason,
            completion.InputSnapshot));
        await SaveStateAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            RoadmapState.SplitChildSelection,
            RoadmapState.EvidenceBlocked,
            runtimePrompt,
            projectionPath,
            evidencePath,
            decision,
            completion.Started,
            completed,
            null,
            [new BlockerRow(reason, $"Review {evidencePath} and rerun the roadmap CLI after resolving the split output.")],
            new RoadmapTransitionIntent("ResolveSplitEpicBlocker", RoadmapState.EvidenceBlocked, [evidencePath]),
            ["Resolve blocker and rerun"]);

        return ArtifactPromotionResult.NotPromoted(status, RoadmapArtifactPaths.ActiveEpic, evidencePath, reason);
    }

    private static string DescribeSplitInterpretation(SplitEpicBundleInterpretation interpretation)
    {
        if (interpretation.Rejections.Count == 0)
        {
            return interpretation.Reason;
        }

        string rejected = string.Join(
            "; ",
            interpretation.Rejections.Select(rejection => $"{rejection.Path}: {rejection.Reason}"));
        return $"{interpretation.Reason} {rejected}";
    }

    private static string RenderSplitInterpretationEvidence(
        SplitEpicBundleInterpretation interpretation,
        string rawOutput)
    {
        var lines = new List<string>
        {
            "# Split Epic Output Blocked",
            string.Empty,
            "## Reason",
            string.Empty,
            DescribeSplitInterpretation(interpretation),
            string.Empty,
            "## Rejected Files",
            string.Empty,
        };

        if (interpretation.Rejections.Count == 0)
        {
            lines.Add("- None");
        }
        else
        {
            foreach (SplitEpicBundleRejection rejection in interpretation.Rejections)
            {
                lines.Add($"- `{rejection.Path}`: {rejection.Reason}");
            }
        }

        lines.AddRange(
        [
            string.Empty,
            "## Raw Output",
            string.Empty,
            "```markdown",
            rawOutput,
            "```",
        ]);

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private async Task GenerateMilestoneSpecsAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "GenerateMilestoneDeepDivesForEpic";
        console.Phase("Generate milestone deep dives");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
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
        await executionPreparation.RecordMilestoneSpecsAsync(
            bundle.Files.Select(file => file.Path).ToArray(),
            cancellationToken);

        InvariantValidationResult invariant = await invariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash, cancellationToken);
        if (!invariant.IsValid)
        {
            await PersistInvariantFailureAndThrowAsync(
                invariant,
                RoadmapState.ActiveEpicReady,
                RoadmapState.MilestoneSpecsReady,
                "PostMilestoneInvariantValidation",
                projection.Definition.ProjectionPath);
        }
    }

    private async Task EnsureExecutionReadinessAsync(
        RoadmapState sourceState,
        CancellationToken cancellationToken)
    {
        DerivedArtifactFreshness operationalContextFreshness = await executionPreparation.EvaluateOperationalContextFreshnessAsync(cancellationToken);
        if (!operationalContextFreshness.IsFresh)
        {
            DateTimeOffset started = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.GenerateOperationalContext,
                TransitionStatus.Started,
                sourceState,
                RoadmapState.GenerateOperationalContext,
                "GenerateOperationalContext",
                "None",
                RoadmapArtifactPaths.OperationalContext,
                "Started",
                started,
                null,
                null,
                null);

            console.Phase("Generate operational context");
            await operationalContextGenerator.GenerateAsync(cancellationToken);
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.OperationalContextReady,
                TransitionStatus.Completed,
                RoadmapState.GenerateOperationalContext,
                RoadmapState.OperationalContextReady,
                "GenerateOperationalContext",
                "None",
                RoadmapArtifactPaths.OperationalContext,
                "Completed",
                started,
                completed,
                null,
                null);
        }
        else if (sourceState is RoadmapState.MilestoneSpecsReady or RoadmapState.GenerateOperationalContext)
        {
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.OperationalContextReady,
                TransitionStatus.Completed,
                sourceState,
                RoadmapState.OperationalContextReady,
                "GenerateOperationalContext",
                "None",
                RoadmapArtifactPaths.OperationalContext,
                "Artifact Ready",
                completed,
                completed,
                null,
                null);
        }

        DerivedArtifactFreshness executionPromptFreshness = await executionPreparation.EvaluateExecutionPromptFreshnessAsync(cancellationToken);
        if (!executionPromptFreshness.IsFresh)
        {
            DateTimeOffset started = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.GenerateExecutionPrompt,
                TransitionStatus.Started,
                RoadmapState.OperationalContextReady,
                RoadmapState.GenerateExecutionPrompt,
                "GenerateExecutionPrompt",
                "None",
                RoadmapArtifactPaths.ExecutionPrompt,
                "Started",
                started,
                null,
                null,
                null);

            console.Phase("Generate execution prompt");
            await executionPromptGenerator.GenerateAsync(cancellationToken);
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.ExecutionPromptReady,
                TransitionStatus.Completed,
                RoadmapState.GenerateExecutionPrompt,
                RoadmapState.ExecutionPromptReady,
                "GenerateExecutionPrompt",
                "None",
                RoadmapArtifactPaths.ExecutionPrompt,
                "Completed",
                started,
                completed,
                null,
                null,
                new RoadmapTransitionIntent(ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution), RoadmapState.ExecutionPromptReady, [RoadmapArtifactPaths.ExecutionPrompt]),
                ["ExecutionLoop", ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution)]);
        }
        else if (sourceState is not (RoadmapState.ExecutionPromptReady or RoadmapState.ExecutionLoop))
        {
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await SaveStateAsync(
                RoadmapState.ExecutionPromptReady,
                TransitionStatus.Completed,
                RoadmapState.OperationalContextReady,
                RoadmapState.ExecutionPromptReady,
                "GenerateExecutionPrompt",
                "None",
                RoadmapArtifactPaths.ExecutionPrompt,
                "Artifact Ready",
                completed,
                completed,
                null,
                null,
                new RoadmapTransitionIntent(ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution), RoadmapState.ExecutionPromptReady, [RoadmapArtifactPaths.ExecutionPrompt]),
                ["ExecutionLoop", ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution)]);
        }

        DerivedArtifactFreshness compatibilityFreshness = await executionPreparation.EvaluateCompatibilityFreshnessAsync(cancellationToken);
        if (!compatibilityFreshness.IsFresh)
        {
            await executionMaterializer.MaterializeAsync(cancellationToken);
        }
    }

    private async Task<RoadmapOutcome> RunExecutionAndCertificationAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        InvariantValidationResult invariant = await invariantValidator.ValidateAsync(RoadmapState.ExecutionPromptReady, projectContext.Hash, cancellationToken);
        if (!invariant.IsValid)
        {
            await PersistInvariantFailureAndThrowAsync(
                invariant,
                RoadmapState.ExecutionPromptReady,
                RoadmapState.ExecutionLoop,
                "PreExecutionInvariantValidation",
                "None");
        }

        DateTimeOffset executionStarted = DateTimeOffset.UtcNow;
        await SaveStateAsync(
            RoadmapState.ExecutionLoop,
            TransitionStatus.Started,
            RoadmapState.ExecutionPromptReady,
            RoadmapState.ExecutionLoop,
            "ExecutionLoop",
            "None",
            RoadmapArtifactPaths.ExecutionPrompt,
            "Execution Started",
            executionStarted,
            null,
            null,
            null,
            new RoadmapTransitionIntent(ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution), RoadmapState.ExecutionLoop, [RoadmapArtifactPaths.ExecutionPrompt]),
            [ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution)]);

        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ActiveEpic, ArtifactLifecycleState.Executing);
        RoadmapExecutionTransportResult transport = await executionBridge.RunAsync(cancellationToken);
        RoadmapExecutionOutcome executionOutcome = executionInterpreter.Interpret(transport);
        string executionEvidencePath = await PersistExecutionEvidenceAsync(transport, executionOutcome);

        return executionOutcome.Kind switch
        {
            RoadmapExecutionOutcomeKind.EpicComplete => await RunCompletionCertificationAsync(
                projectContext,
                executionStarted,
                executionEvidencePath,
                cancellationToken,
                completionRoute: executionOutcome.RequireValidatedRoute()),
            RoadmapExecutionOutcomeKind.ContinueRequired => await PersistExecutionContinuationAsync(
                executionOutcome,
                executionStarted,
                executionEvidencePath),
            RoadmapExecutionOutcomeKind.ExecutionBlocked => await PersistExecutionBlockedAsync(
                executionOutcome,
                executionStarted,
                executionEvidencePath),
            RoadmapExecutionOutcomeKind.MalformedOutput => await PersistMalformedExecutionOutputAsync(
                executionOutcome,
                executionStarted,
                executionEvidencePath),
            RoadmapExecutionOutcomeKind.RuntimeFailure => await PersistExecutionRuntimeFailureAsync(
                executionOutcome,
                executionStarted,
                executionEvidencePath),
            _ => throw new RoadmapStepException($"Unsupported execution outcome: {executionOutcome.Kind}."),
        };
    }

    private async Task<RoadmapOutcome> RunCompletionCertificationAsync(
        ProjectContext projectContext,
        DateTimeOffset executionStarted,
        string executionEvidencePath,
        CancellationToken cancellationToken,
        bool persistCompletionClaim = true,
        ExecutionDispositionRoute? completionRoute = null)
    {
        DateTimeOffset executionCompleted = DateTimeOffset.UtcNow;
        if (persistCompletionClaim)
        {
            ExecutionDispositionRoute completionClaimRoute = completionRoute
                ?? throw new RoadmapStepException("Completion certification requires a validated execution completion route.");
            await SaveStateAsync(
                completionClaimRoute.TargetState,
                TransitionStatus.Completed,
                RoadmapState.ExecutionLoop,
                completionClaimRoute.TargetState,
                "ExecutionOutcomeInterpretation",
                "None",
                executionEvidencePath,
                ExecutionDispositionProtocol.StatusText(completionClaimRoute.Status),
                executionStarted,
                executionCompleted,
                null,
                null,
                new RoadmapTransitionIntent(completionClaimRoute.WorkflowTransition, completionClaimRoute.TargetState, [executionEvidencePath]),
                [completionClaimRoute.WorkflowTransition]);
        }

        string runtimePrompt = ExecutionDispositionProtocol.CommandText(ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift);
        console.Phase("Evaluate epic completion and drift");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionEvaluationContextAsync(projection.Content, executionEvidencePath);
        string output = await RunPromptTransitionAsync(
            RoadmapState.EpicCompletionDetected,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.EvaluationEvidenceDirectory],
            cancellationToken,
            TransitionInputContext.ExecutionEvidence(executionEvidencePath));
        string evaluationPath = await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "epic-completion-and-drift", output);
        CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(output);
        CompletionCertificationPolicyResult certification = completionPolicy.Validate(decision);
        await AppendDecisionAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, evaluationPath, decision.ClosureRecommendation, "Unclear", decision.OverallCompletionStatus);

        if (!certification.IsValid)
        {
            return await PersistInvalidCompletionCertificationAsync(
                certification,
                projection.Definition.ProjectionPath,
                evaluationPath);
        }

        CompletionCertificationRoute route = completionRouter.Route(certification.Decision);
        if (route.RequiresRoadmapCompletionContextUpdate)
        {
            await UpdateRoadmapCompletionContextAsync(projectContext, evaluationPath, cancellationToken);
        }

        if (route.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                activeEpicLifecycleState,
                $"Completion certification route: {route.ClosureRecommendation}");
        }

        await PersistCompletionRouteAsync(route, decision, projection.Definition.ProjectionPath, evaluationPath);
        return route.CliOutcome;
    }

    private async Task<string> ReadPersistedExecutionEvidencePathAsync()
    {
        RoadmapStateDocument? state = await stateStore.LoadAsync();
        IReadOnlyList<string> candidates = (state?.TransitionIntent.EvidencePaths ?? [])
            .Concat(OutputEvidencePaths(state?.LastTransition.Output ?? string.Empty))
            .Where(path => path.StartsWith(RoadmapArtifactPaths.ExecutionEvidenceDirectory, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (string path in candidates)
        {
            if (await artifacts.GetStatusAsync(path) == ArtifactStatus.Present)
            {
                return path;
            }
        }

        throw new RoadmapStepException("Cannot resume completion certification because execution evidence is missing.");
    }

    private async Task<string> PersistExecutionEvidenceAsync(
        RoadmapExecutionTransportResult transport,
        RoadmapExecutionOutcome outcome)
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.ExecutionEvidenceDirectory,
            "execution-turn",
            RoadmapExecutionEvidenceArtifact.Render(transport, outcome, createdAt));
        await lifecycleStore.UpsertAsync(
            evidencePath,
            outcome.Kind is RoadmapExecutionOutcomeKind.EpicComplete or RoadmapExecutionOutcomeKind.ContinueRequired
                ? ArtifactLifecycleState.Ready
                : ArtifactLifecycleState.Blocked,
            $"Execution outcome: {outcome.DecisionText}");
        return evidencePath;
    }

    private async Task<RoadmapOutcome> PersistExecutionContinuationAsync(
        RoadmapExecutionOutcome outcome,
        DateTimeOffset executionStarted,
        string executionEvidencePath)
    {
        ExecutionDispositionRoute route = outcome.RequireValidatedRoute();
        DateTimeOffset completed = DateTimeOffset.UtcNow;
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.ActiveEpic,
            ArtifactLifecycleState.Executing,
            "Execution requested continuation.");
        await SaveStateAsync(
            route.TargetState,
            TransitionStatus.Paused,
            RoadmapState.ExecutionLoop,
            route.TargetState,
            "ExecutionOutcomeInterpretation",
            "None",
            executionEvidencePath,
            outcome.DecisionText,
            executionStarted,
            completed,
            null,
            null,
            new RoadmapTransitionIntent(route.WorkflowTransition, route.TargetState, [RoadmapArtifactPaths.ExecutionPrompt, executionEvidencePath]),
            ["ExecutionLoop", route.WorkflowTransition]);
        return RoadmapOutcome.Paused;
    }

    private async Task<RoadmapOutcome> PersistExecutionBlockedAsync(
        RoadmapExecutionOutcome outcome,
        DateTimeOffset executionStarted,
        string executionEvidencePath)
    {
        ExecutionDispositionRoute route = outcome.RequireValidatedRoute();
        DateTimeOffset completed = DateTimeOffset.UtcNow;
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.ActiveEpic,
            ArtifactLifecycleState.Executing,
            "Execution blocked; active epic remains in execution lifecycle.");
        await SaveStateAsync(
            route.TargetState,
            TransitionStatus.Paused,
            RoadmapState.ExecutionLoop,
            route.TargetState,
            "ExecutionOutcomeInterpretation",
            "None",
            executionEvidencePath,
            outcome.DecisionText,
            executionStarted,
            completed,
            null,
            [new BlockerRow(outcome.Message, $"Review {executionEvidencePath}, resolve the execution blocker, and rerun.")],
            new RoadmapTransitionIntent(route.WorkflowTransition, route.TargetState, [executionEvidencePath]),
            ["Resolve execution blocker and rerun"]);
        return RoadmapOutcome.Paused;
    }

    private async Task<RoadmapOutcome> PersistMalformedExecutionOutputAsync(
        RoadmapExecutionOutcome outcome,
        DateTimeOffset executionStarted,
        string executionEvidencePath)
    {
        DateTimeOffset completed = DateTimeOffset.UtcNow;
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.ActiveEpic,
            ArtifactLifecycleState.Executing,
            "Execution output was malformed; active epic remains executing.");
        await SaveStateAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            RoadmapState.ExecutionLoop,
            RoadmapState.EvidenceBlocked,
            "ExecutionOutcomeInterpretation",
            "None",
            executionEvidencePath,
            outcome.DecisionText,
            executionStarted,
            completed,
            null,
            [new BlockerRow(outcome.Message, $"Review {executionEvidencePath}, repair the execution disposition output, and rerun.")],
            new RoadmapTransitionIntent("ResolveMalformedExecutionOutput", RoadmapState.EvidenceBlocked, [executionEvidencePath]),
            ["Resolve malformed execution output and rerun"]);
        return RoadmapOutcome.Paused;
    }

    private async Task<RoadmapOutcome> PersistExecutionRuntimeFailureAsync(
        RoadmapExecutionOutcome outcome,
        DateTimeOffset executionStarted,
        string executionEvidencePath)
    {
        DateTimeOffset failed = DateTimeOffset.UtcNow;
        await SaveStateAsync(
            RoadmapState.Failed,
            TransitionStatus.Failed,
            RoadmapState.ExecutionLoop,
            RoadmapState.Failed,
            "ExecutionLoop",
            "None",
            executionEvidencePath,
            outcome.DecisionText,
            executionStarted,
            failed,
            null,
            [new BlockerRow(outcome.Message, $"Review {executionEvidencePath}, repair the execution runtime failure, and rerun.")],
            new RoadmapTransitionIntent("RepairExecutionRuntimeFailure", RoadmapState.Failed, [executionEvidencePath]),
            ["Repair execution runtime failure and rerun"]);
        return RoadmapOutcome.Failed;
    }

    private async Task UpdateRoadmapCompletionContextAsync(ProjectContext projectContext, string evaluationPath, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "UpdateRoadmapCompletionContext";
        console.Phase("Update roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionUpdateContextAsync(projection.Content, evaluationPath);
        string output = await RunPromptTransitionAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken,
            TransitionInputContext.CompletionEvaluation(evaluationPath));
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "roadmap-completion-update", output);
        await SupersedeActiveSelectionAsync(
            [DerivedArtifactStaleReason.RoadmapCompletionContextDrift],
            "Roadmap completion context changed after completion certification.");
        await AppendDecisionAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.RoadmapCompletionContext, "Roadmap Completion Context Updated", "Unclear", "Completion context updated after certification.");
    }

    private async Task<PromptTransitionCompletion> RunPromptForPromotionAsync(
        RoadmapState from,
        RoadmapState promotionTarget,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        TransitionInputSnapshot inputSnapshot = await inputResolver.ResolveAsync(new TransitionInputRequest(
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            inputContext ?? TransitionInputContext.Empty));
        string correlationId = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        string outputList = string.Join(", ", outputs);
        await SaveStateAsync(from, TransitionStatus.Started, from, promotionTarget, prompt, projectionPath, outputList, "Prompt Started", started, null, null, null);
        await journalStore.AppendAsync(new TransitionJournalRecord("TransitionStarted", correlationId, started, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, 0, "Started", "None", null, inputSnapshot));

        try
        {
            string output = await promptRunner.RunRuntimePromptAsync(prompt, projectContext, secondaryInput, cancellationToken);
            stopwatch.Stop();
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("PromptCompleted", correlationId, completed, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "PromptCompleted", "Output produced", null, inputSnapshot));
            await SaveStateAsync(from, TransitionStatus.PromptCompleted, from, promotionTarget, prompt, projectionPath, outputList, "Prompt Completed", started, completed, null, null);
            return new PromptTransitionCompletion(correlationId, started, completed, stopwatch.ElapsedMilliseconds, output, inputSnapshot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            DateTimeOffset failed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("TransitionFailed", correlationId, failed, from, promotionTarget, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Failed", "None", exception.Message, inputSnapshot));
            await SaveStateAsync(
                RoadmapState.EvidenceBlocked,
                TransitionStatus.Failed,
                from,
                promotionTarget,
                prompt,
                projectionPath,
                outputList,
                "Runtime Failure",
                started,
                failed,
                null,
                [new BlockerRow(exception.Message, "Review the transition failure and rerun.")],
                new RoadmapTransitionIntent("ResolveTransitionFailure", RoadmapState.EvidenceBlocked, outputs),
                ["Resolve blocker and rerun"]);
            throw RoadmapStepException.AlreadyPersisted(exception);
        }
    }

    private async Task<ArtifactPromotionResult> PromoteActiveEpicAsync(
        RoadmapState from,
        string prompt,
        string projectionPath,
        PromptTransitionCompletion completion,
        string? lifecycleNotes = null)
    {
        ArtifactPromotionResult result = await promotionService.PromoteAsync(new ArtifactPromotionRequest(
            RoadmapArtifactPaths.ActiveEpic,
            completion.Output,
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "active-epic-promotion",
            "active epic",
            new EpicAuthoringOutputClassifier(),
            new EpicArtifactValidator(),
            ArtifactLifecycleState.Ready,
            lifecycleNotes ?? $"Promoted by {prompt}."));

        DateTimeOffset completed = DateTimeOffset.UtcNow;
        if (result.Promoted)
        {
            await journalStore.AppendAsync(new TransitionJournalRecord(
                "ArtifactPromoted",
                completion.CorrelationId,
                completed,
                from,
                RoadmapState.ActiveEpicReady,
                prompt,
                projectionPath,
                "ArtifactPromotionService",
                completion.InputSnapshot.ToInputArtifactHashes(),
                [RoadmapArtifactPaths.ActiveEpic],
                completion.ElapsedMilliseconds,
                "Promoted",
                "Active epic promoted",
                null,
                completion.InputSnapshot));
            await SaveStateAsync(RoadmapState.ActiveEpicReady, TransitionStatus.Completed, from, RoadmapState.ActiveEpicReady, prompt, projectionPath, RoadmapArtifactPaths.ActiveEpic, "Artifact Promoted", completion.Started, completed, null, null);
            return result;
        }

        string evidencePath = result.EvidencePath ?? RoadmapArtifactPaths.BlockerEvidenceDirectory;
        string decision = result.Status switch
        {
            ArtifactPromotionStatus.Blocked => "Artifact Promotion Blocked",
            ArtifactPromotionStatus.Ambiguous => "Artifact Promotion Ambiguous",
            ArtifactPromotionStatus.StructurallyInvalid => "Artifact Promotion Invalid",
            _ => "Artifact Promotion Rejected",
        };

        await journalStore.AppendAsync(new TransitionJournalRecord(
            "ArtifactPromotionBlocked",
            completion.CorrelationId,
            completed,
            from,
            RoadmapState.ActiveEpicReady,
            prompt,
            projectionPath,
            "ArtifactPromotionService",
            completion.InputSnapshot.ToInputArtifactHashes(),
            [evidencePath],
            completion.ElapsedMilliseconds,
            result.Status.ToString(),
            decision,
            result.Reason,
            completion.InputSnapshot));
        await SaveStateAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            from,
            RoadmapState.ActiveEpicReady,
            prompt,
            projectionPath,
            evidencePath,
            decision,
            completion.Started,
            completed,
            null,
            [new BlockerRow(result.Reason, $"Review {evidencePath} and rerun the roadmap CLI after resolving the blocker.")],
            new RoadmapTransitionIntent("ResolveArtifactPromotionBlocker", RoadmapState.EvidenceBlocked, [evidencePath]),
            ["Resolve blocker and rerun"]);
        return result;
    }

    private async Task<string> RunPromptTransitionAsync(
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        PromptTransitionCompletion completion = await RunPromptTransitionWithCompletionAsync(
            from,
            to,
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            outputs,
            cancellationToken,
            inputContext);
        return completion.Output;
    }

    private async Task<PromptTransitionCompletion> RunPromptTransitionWithCompletionAsync(
        RoadmapState from,
        RoadmapState to,
        string prompt,
        string projectionPath,
        string projectContext,
        string secondaryInput,
        IReadOnlyList<string> outputs,
        CancellationToken cancellationToken,
        TransitionInputContext? inputContext = null)
    {
        TransitionInputSnapshot inputSnapshot = await inputResolver.ResolveAsync(new TransitionInputRequest(
            prompt,
            projectionPath,
            projectContext,
            secondaryInput,
            inputContext ?? TransitionInputContext.Empty));
        string correlationId = Guid.NewGuid().ToString("N");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        await SaveStateAsync(to, TransitionStatus.Started, from, to, prompt, projectionPath, string.Join(", ", outputs), "Pending", started, null, null, null);
        await journalStore.AppendAsync(new TransitionJournalRecord("TransitionStarted", correlationId, started, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, 0, "Started", "None", null, inputSnapshot));

        try
        {
            string output = await promptRunner.RunRuntimePromptAsync(prompt, projectContext, secondaryInput, cancellationToken);
            stopwatch.Stop();
            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("TransitionCompleted", correlationId, completed, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Completed", "None", null, inputSnapshot));
            await SaveStateAsync(to, TransitionStatus.Completed, from, to, prompt, projectionPath, string.Join(", ", outputs), "Completed", started, completed, null, null);
            return new PromptTransitionCompletion(correlationId, started, completed, stopwatch.ElapsedMilliseconds, output, inputSnapshot);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();
            DateTimeOffset failed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord("TransitionFailed", correlationId, failed, from, to, prompt, projectionPath, prompt, inputSnapshot.ToInputArtifactHashes(), outputs, stopwatch.ElapsedMilliseconds, "Failed", "None", exception.Message, inputSnapshot));
            await SaveStateAsync(
                RoadmapState.EvidenceBlocked,
                TransitionStatus.Failed,
                from,
                to,
                prompt,
                projectionPath,
                string.Join(", ", outputs),
                "Failed",
                started,
                failed,
                null,
                [new BlockerRow(exception.Message, "Review the transition failure and rerun.")],
                new RoadmapTransitionIntent("ResolveTransitionFailure", RoadmapState.EvidenceBlocked, outputs),
                ["Resolve blocker and rerun"]);
            throw RoadmapStepException.AlreadyPersisted(exception);
        }
    }

    private async Task PersistCompletionRouteAsync(
        CompletionCertificationRoute route,
        CompletionEvaluationDecision decision,
        string projectionPath,
        string evaluationPath,
        IReadOnlyList<string>? additionalEvidencePaths = null,
        IReadOnlyList<BlockerRow>? blockers = null)
    {
        DateTimeOffset completed = DateTimeOffset.UtcNow;
        IReadOnlyList<string> extraOutputs = additionalEvidencePaths ?? [];
        IReadOnlyList<string> outputs = route.RequiresRoadmapCompletionContextUpdate
            ? [evaluationPath, RoadmapArtifactPaths.RoadmapCompletionContext, ..extraOutputs]
            : [evaluationPath, ..extraOutputs];
        string routingContext = string.Join(
            Environment.NewLine,
            [
                $"Closure Recommendation: {decision.ClosureRecommendation}",
                $"Overall Completion Status: {decision.OverallCompletionStatus}",
                $"Overall Drift Classification: {decision.OverallDriftClassification}",
                $"Target State: {route.TargetState}",
                $"Transition Status: {route.TransitionStatus}",
            ]);
        TransitionInputSnapshot inputSnapshot = await inputResolver.ResolveAsync(new TransitionInputRequest(
            "CompletionCertificationRouting",
            projectionPath,
            routingContext,
            string.Empty,
            TransitionInputContext.CompletionEvaluation(evaluationPath)));
        await journalStore.AppendAsync(new TransitionJournalRecord(
            "TransitionCompleted",
            Guid.NewGuid().ToString("N"),
            completed,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            route.TargetState,
            "CompletionCertificationRouting",
            projectionPath,
            "CompletionCertificationRouter",
            inputSnapshot.ToInputArtifactHashes(),
            outputs,
            0,
            route.TransitionStatus.ToString(),
            decision.ClosureRecommendation,
            null,
            inputSnapshot));
        await SaveStateAsync(
            route.TargetState,
            route.TransitionStatus,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            route.TargetState,
            "CompletionCertificationRouting",
            projectionPath,
            string.Join(", ", outputs),
            decision.ClosureRecommendation,
            completed,
            completed,
            null,
            blockers,
            extraOutputs.Count > 0
                ? new RoadmapTransitionIntent(route.Intent.ToString(), route.TargetState, outputs)
                : route.ToRoadmapTransitionIntent(evaluationPath),
            route.NextTransitions);
    }

    private async Task<RoadmapOutcome> PersistInvalidCompletionCertificationAsync(
        CompletionCertificationPolicyResult certification,
        string projectionPath,
        string evaluationPath)
    {
        DateTimeOffset blockedAt = DateTimeOffset.UtcNow;
        string reason = certification.RejectionReason ?? "Completion certification failed semantic policy validation.";
        string requiredNextStep = $"Review {evaluationPath}, preserve the certification evidence, correct the certification decision, and rerun the roadmap CLI.";
        string blockedTransition = $"Rejected closure recommendation: {certification.Decision.ClosureRecommendation}";
        string details = $"""
            Completion certification was parsed successfully, but the parsed decision failed semantic policy validation.

            ## Parsed Decision

            | Field | Value |
            |---|---|
            | Overall Completion Status | {certification.Decision.OverallCompletionStatus} |
            | Overall Drift Classification | {certification.Decision.OverallDriftClassification} |
            | Closure Recommendation | {certification.Decision.ClosureRecommendation} |

            ## Validation Failure

            {reason}

            ## Blocked Transition

            | Field | Value |
            |---|---|
            | Rejected Closure Recommendation | {certification.Decision.ClosureRecommendation} |
            | Route Selection | Blocked before workflow routing |
            | Lifecycle Mutation | Blocked before mutation |
            | Roadmap Completion Context Update | Blocked before mutation |

            ## Preserved Evidence

            | Field | Value |
            |---|---|
            | Raw Certification Artifact | {evaluationPath} |
            | Blocked Transition | {blockedTransition} |
            | Required Next Step | {requiredNextStep} |
            """;
        string blockerPath = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "invalid-completion-certification",
            RoadmapBlockedArtifact.Render(
                RoadmapState.EvidenceBlocked,
                "CompletionCertificationRouting",
                OneLine(reason),
                requiredNextStep,
                evaluationPath,
                details,
                blockedAt));
        string routingContext = string.Join(
            Environment.NewLine,
            [
                $"Closure Recommendation: {certification.Decision.ClosureRecommendation}",
                $"Overall Completion Status: {certification.Decision.OverallCompletionStatus}",
                $"Overall Drift Classification: {certification.Decision.OverallDriftClassification}",
                $"Blocked Transition: {blockedTransition}",
                $"Validation Failure: {reason}",
            ]);
        TransitionInputSnapshot inputSnapshot = await inputResolver.ResolveAsync(new TransitionInputRequest(
            "CompletionCertificationRouting",
            projectionPath,
            routingContext,
            string.Empty,
            TransitionInputContext.CompletionEvaluation(evaluationPath)));
        IReadOnlyList<string> outputs = [evaluationPath, blockerPath];
        await journalStore.AppendAsync(new TransitionJournalRecord(
            "CompletionCertificationRejected",
            Guid.NewGuid().ToString("N"),
            blockedAt,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.EvidenceBlocked,
            "CompletionCertificationRouting",
            projectionPath,
            "CompletionCertificationPolicy",
            inputSnapshot.ToInputArtifactHashes(),
            outputs,
            0,
            TransitionStatus.Paused.ToString(),
            "Invalid Completion Certification",
            reason,
            inputSnapshot));
        await SaveStateAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.EvidenceBlocked,
            "CompletionCertificationRouting",
            projectionPath,
            string.Join(", ", outputs),
            "Invalid Completion Certification",
            blockedAt,
            blockedAt,
            null,
            [new BlockerRow(OneLine(reason), requiredNextStep)],
            new RoadmapTransitionIntent("ResolveInvalidCompletionCertification", RoadmapState.EvidenceBlocked, outputs),
            ["Resolve invalid completion certification and rerun"]);

        return RoadmapOutcome.Paused;
    }

    internal async Task PersistInvariantFailureAndThrowAsync(
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
        string fallbackPath = await artifacts.WriteNumberedEvidenceAsync(
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

    private async Task PersistWorkflowFailureAsync(RoadmapWorkflowFailure failure)
    {
        IReadOnlyDictionary<string, string> inputHashes = failure.InputSnapshot?.ToInputArtifactHashes()
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        await journalStore.AppendAsync(new TransitionJournalRecord(
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

        await SaveStateAsync(
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

    private async Task<string> ReadCurrentSelectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string selection = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.Selection);
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"];
        string? selectionProjection = await artifacts.ReadAsync(projectionPath);
        if (string.IsNullOrWhiteSpace(selectionProjection))
        {
            throw new RoadmapStepException("Active selection cannot be used because its SelectNextEpic projection is missing.");
        }

        RoadmapStateDocument? state = await stateStore.LoadAsync();
        TransitionInputSnapshot currentCycle = await selectionProvenance.CaptureCurrentCycleAsync(
            selectionProjection,
            state?.RetiredEpics ?? [],
            cancellationToken);
        DerivedArtifactFreshness freshness = await selectionProvenance.EvaluateActiveSelectionFreshnessAsync(
            currentCycle,
            state?.RetiredEpics ?? [],
            cancellationToken);
        if (!freshness.IsFresh)
        {
            throw new RoadmapStepException(
                $"Active selection cannot be used because it does not belong to the current selection cycle: {FormatReasons(freshness.Reasons)}.");
        }

        return selection;
    }

    private async Task SupersedeActiveSelectionAsync(
        IReadOnlyList<DerivedArtifactStaleReason> reasons,
        string lifecycleNotes)
    {
        await selectionProvenance.SupersedeActiveSelectionAsync(reasons);
        await lifecycleStore.UpsertAsync(
            RoadmapArtifactPaths.Selection,
            ArtifactLifecycleState.Superseded,
            lifecycleNotes);
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
        IReadOnlyList<RetiredEpic>? retiredEpics,
        IReadOnlyList<BlockerRow>? blockers,
        RoadmapTransitionIntent? transitionIntent = null,
        IReadOnlyList<string>? nextTransitions = null)
    {
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        ProjectionManifest manifest = await manifestStore.LoadAsync();
        IReadOnlyList<ArtifactStateRow> activeArtifacts = await ActiveArtifactRowsAsync();
        string lastDecision = await decisionLedger.LastDecisionIdAsync();
        IReadOnlyList<RetiredEpic> effectiveRetiredEpics = retiredEpics ?? existing?.RetiredEpics ?? [];
        IReadOnlyList<BlockerRow> effectiveBlockers = blockers ?? existing?.Blockers ?? [];
        int splitFamilyCount = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md")).Count;
        await stateStore.SaveAsync(new RoadmapStateDocument(
            current,
            activeArtifacts,
            new RoadmapTransitionSummary(from, to, prompt, projection, output, decision, status, started, completed),
            effectiveBlockers,
            lastDecision,
            effectiveRetiredEpics.Count,
            splitFamilyCount,
            new ProjectionManifestCounts(
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Valid),
                manifest.Entries.Count(entry => entry.StaleStatus != ProjectionStaleStatus.Fresh),
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Invalid)),
            transitionIntent ?? existing?.TransitionIntent ?? RoadmapTransitionIntent.Empty(current),
            nextTransitions ?? NextTransitions(current),
            effectiveRetiredEpics));
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
            RoadmapState.SelectNextStrategicInitiative => ["SelectNextEpic"],
            RoadmapState.ActiveEpicReady => ["GenerateMilestoneDeepDives"],
            RoadmapState.MilestoneSpecsReady => ["GenerateOperationalContext"],
            RoadmapState.ExecutionPromptReady => ["ExecutionLoop"],
            RoadmapState.ExecutionLoop => [ExecutionCommandText(ExecutionDispositionCommand.ContinueExecution)],
            RoadmapState.EpicPreparationAudit => ["EpicPreparationAudit"],
            RoadmapState.RetireEpic => ["SelectNextStrategicInitiative"],
            RoadmapState.EvidenceGathering => ["GatherAdditionalEvidence", ExecutionCommandText(ExecutionDispositionCommand.EvaluateEpicCompletionAndDrift)],
            RoadmapState.EvidenceBlocked => ["Resolve blocker and rerun"],
            _ => [],
        };

    private static string ExecutionCommandText(ExecutionDispositionCommand command) =>
        ExecutionDispositionProtocol.CommandText(command);

    private async Task WriteBlockedStateAsync(RoadmapState state, string transition, string reason)
    {
        string blockerReason = OneLine(reason);
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "roadmap-transition-blocked",
            RoadmapBlockedArtifact.Render(state, transition, blockerReason, "Address the blocker and rerun the roadmap CLI.", "None", reason, DateTimeOffset.UtcNow));
        await SaveStateAsync(
            state,
            TransitionStatus.Failed,
            RoadmapState.CoreReady,
            state,
            transition,
            "None",
            path,
            "Blocked",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            [new BlockerRow(blockerReason, "Address the blocker and rerun the roadmap CLI.")],
            new RoadmapTransitionIntent("ResolveBlocker", state, [path]),
            ["Resolve blocker and rerun"]);
    }

    private async Task WritePreflightInterruptedStateAsync(RoadmapStateDocument interruptedState, string reason)
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        string blockerReason = OneLine(reason);
        string details = $"""
            Project Context preflight failed before runtime execution could resume.

            ## Interrupted Workflow

            | Field | Value |
            |---|---|
            | Previous Current State | {interruptedState.CurrentState} |
            | Previous Last Transition | {interruptedState.LastTransition.Prompt} |
            | Previous Transition Status | {interruptedState.LastTransition.Status} |
            | Previous Transition Intent | {interruptedState.TransitionIntent.Intent} |
            | Previous Dispatch State | {interruptedState.TransitionIntent.DispatchState} |
            | Previous Evidence Paths | {FormatList(interruptedState.TransitionIntent.EvidencePaths)} |

            ## Previous Blockers

            {FormatBlockers(interruptedState.Blockers)}

            ## Preflight Failure

            {reason}
            """;
        string path = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "roadmap-preflight-blocked",
            RoadmapBlockedArtifact.Render(
                interruptedState.CurrentState,
                "Preflight",
                blockerReason,
                "Repair Project Context and rerun the roadmap CLI; the interrupted workflow context remains authoritative.",
                RoadmapArtifactPaths.State,
                details,
                createdAt));

        ProjectionManifest manifest = await manifestStore.LoadAsync();
        int splitFamilyCount = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md")).Count;
        await stateStore.SaveAsync(interruptedState with
        {
            ActiveArtifacts = await ActiveArtifactRowsAsync(),
            Blockers = AppendPreflightBlocker(interruptedState.Blockers, blockerReason, path),
            LastDecisionId = await decisionLedger.LastDecisionIdAsync(),
            RetiredEpicsCount = interruptedState.RetiredEpics.Count,
            SplitFamiliesCount = splitFamilyCount,
            ProjectionManifestCounts = new ProjectionManifestCounts(
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Valid),
                manifest.Entries.Count(entry => entry.StaleStatus != ProjectionStaleStatus.Fresh),
                manifest.Entries.Count(entry => entry.ValidationStatus == ProjectionValidationStatus.Invalid)),
            NextValidTransitions = AppendNextTransition(
                interruptedState.NextValidTransitions,
                "Repair Project Context and rerun"),
        });
    }

    private async Task WriteCancelledStateAsync()
    {
        RoadmapStateDocument? existing = await stateStore.LoadAsync();
        RoadmapTransitionSummary interrupted = existing?.LastTransition ?? new RoadmapTransitionSummary(
            RoadmapState.CoreReady,
            RoadmapState.Cancelled,
            "Cancellation",
            "None",
            "None",
            "Cancelled",
            TransitionStatus.Cancelled,
            DateTimeOffset.UtcNow,
            null);

        RoadmapState recoveryState = existing?.CurrentState is { } current && current != RoadmapState.Cancelled
            ? current
            : interrupted.From;
        DateTimeOffset cancelledAt = DateTimeOffset.UtcNow;
        await SaveStateAsync(
            RoadmapState.Cancelled,
            TransitionStatus.Cancelled,
            interrupted.From,
            RoadmapState.Cancelled,
            interrupted.Prompt,
            interrupted.Projection,
            interrupted.Output,
            "Cancelled",
            interrupted.StartedAt == DateTimeOffset.MinValue ? cancelledAt : interrupted.StartedAt,
            cancelledAt,
            null,
            [new BlockerRow("Cancelled", "Rerun the roadmap CLI when ready.")],
            new RoadmapTransitionIntent("ResumeCancelledTransition", recoveryState, OutputEvidencePaths(interrupted.Output)),
            ["Resume cancelled transition"]);
    }

    private static IReadOnlyList<string> OutputEvidencePaths(string output)
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

    private static IReadOnlyList<BlockerRow> AppendPreflightBlocker(
        IReadOnlyList<BlockerRow> existing,
        string reason,
        string evidencePath)
    {
        if (existing.Any(blocker => string.Equals(blocker.Blocker, reason, StringComparison.Ordinal)))
        {
            return existing;
        }

        return
        [
            ..existing,
            new BlockerRow(
                reason,
                $"Repair Project Context and rerun the roadmap CLI. See {evidencePath}."),
        ];
    }

    private static IReadOnlyList<string> AppendNextTransition(
        IReadOnlyList<string> existing,
        string transition)
    {
        if (existing.Contains(transition, StringComparer.Ordinal))
        {
            return existing;
        }

        return [..existing, transition];
    }

    private static IReadOnlyList<BlockerRow> AppendUnblockReviewBlocker(
        IReadOnlyList<BlockerRow> existing,
        RoadmapUnblockPlan unblockPlan,
        string reviewPath)
    {
        string blocker = $"{unblockPlan.Decision}: {OneLine(unblockPlan.Reason)}";
        if (existing.Any(row => string.Equals(row.Blocker, blocker, StringComparison.Ordinal)))
        {
            return existing;
        }

        return
        [
            ..existing,
            new BlockerRow(
                blocker,
                $"{unblockPlan.RequiredNextStep} Review evidence: {reviewPath}."),
        ];
    }

    private static string RenderUnblockReviewEvidence(
        RoadmapStateDocument state,
        RoadmapUnblockPlan unblockPlan,
        bool success,
        DateTimeOffset createdAt)
    {
        var lines = new List<string>
        {
            "# Roadmap Unblock Review",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Result | {(success ? "Recovered" : unblockPlan.Status)} |",
            $"| Reviewed State | {state.CurrentState} |",
            $"| Reviewed Intent | {Escape(unblockPlan.TransitionIntent.Intent)} |",
            $"| Dispatch State | {unblockPlan.TransitionIntent.DispatchState} |",
            $"| Recovery Action | {unblockPlan.Action} |",
            $"| Recovery Decision | {Escape(unblockPlan.Decision)} |",
            $"| Target State | {(unblockPlan.TargetState?.ToString() ?? "None")} |",
            $"| Reason | {Escape(unblockPlan.Reason)} |",
            $"| Required Next Step | {Escape(unblockPlan.RequiredNextStep)} |",
            $"| Created At | {createdAt:O} |",
            string.Empty,
            "## Prior Transition",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| From | {state.LastTransition.From} |",
            $"| To | {state.LastTransition.To} |",
            $"| Prompt | {Escape(state.LastTransition.Prompt)} |",
            $"| Projection | {Escape(state.LastTransition.Projection)} |",
            $"| Output | {Escape(state.LastTransition.Output)} |",
            $"| Decision | {Escape(state.LastTransition.Decision)} |",
            $"| Status | {state.LastTransition.Status} |",
            string.Empty,
            "## Original Blockers",
            string.Empty,
            FormatBlockers(state.Blockers),
            string.Empty,
            "## Evidence Hashes",
            string.Empty,
            "| Kind | Path | Status | SHA-256 |",
            "|---|---|---|---|",
        };

        foreach (RoadmapUnblockEvidence evidence in unblockPlan.Evidence)
        {
            lines.Add($"| {Escape(evidence.Kind)} | {Escape(evidence.Path)} | {Escape(evidence.Status)} | {Escape(evidence.Hash)} |");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatBlockers(IReadOnlyList<BlockerRow> blockers)
    {
        if (blockers.Count == 0)
        {
            return "None";
        }

        return string.Join(
            Environment.NewLine,
            blockers.Select(blocker => $"- {blocker.Blocker} Required next step: {blocker.RequiredNextStep}"));
    }

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "None" : string.Join(", ", values);

    private static bool IsBlockedRecoveryState(RoadmapState state) =>
        state is RoadmapState.EvidenceBlocked
            or RoadmapState.Failed
            or RoadmapState.ExecutionBlocked;

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private static string OneLine(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string FormatReasons(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        reasons.Count == 0 ? "UnknownProvenance" : string.Join(", ", reasons);

    private enum EpicPreparationResult
    {
        ActiveEpicReady,
        Retired,
        Blocked,
    }

    private sealed record PromptTransitionCompletion(
        string CorrelationId,
        DateTimeOffset Started,
        DateTimeOffset Completed,
        long ElapsedMilliseconds,
        string Output,
        TransitionInputSnapshot InputSnapshot);
}
