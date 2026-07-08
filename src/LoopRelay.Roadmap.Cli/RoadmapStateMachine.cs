using LoopRelay.Completion;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapStateMachine(
    RoadmapArtifacts artifacts,
    ProjectContextLoader projectContextLoader,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    TransitionInputResolver inputResolver,
    CompletionCertificationPolicy completionPolicy,
    CompletionCertificationRouter completionRouter,
    ICompletedEpicArchiveService completionArchive,
    RoadmapStateStore stateStore,
    RoadmapTransitionPersistence transitionPersistence,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    BootstrapRoadmapCompletionContextTransition bootstrapRoadmapCompletionContextTransition,
    SelectNextEpicTransition selectNextEpicTransition,
    CreateNewEpicTransition createNewEpicTransition,
    EpicPreparationAuditTransition epicPreparationAuditTransition,
    SplitEpicTransition splitEpicTransition,
    GenerateMilestoneDeepDivesTransition generateMilestoneDeepDivesTransition,
    ActiveSelectionReader activeSelectionReader,
    RoadmapStartupPlanner startupPlanner,
    RoadmapResumePlanner resumePlanner,
    RoadmapUnblockPlanner unblockPlanner,
    SelectionSuperseder selectionSuperseder,
    DecisionRecorder decisionRecorder,
    TransitionJournalStore journalStore,
    ArtifactLifecycleStore lifecycleStore,
    ILoopConsole console,
    HitlArtifactCapture hitlArtifactCapture,
    INonImplementationCompletionReviewService? nonImplementationCompletionReview = null)
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
            await ReportEphemeralBlockerAsync("Project Context preflight", exception.Message, persistedState?.CurrentState);
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
            await ReportEphemeralBlockerAsync("Roadmap state machine", exception.Message);
            console.Error(exception.Message);
            return RoadmapOutcome.Failed;
        }
        catch (Exception exception)
        {
            await ReportEphemeralBlockerAsync("Roadmap state machine", exception.Message);
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
                string selectionOutput = await activeSelectionReader.ReadAsync(cancellationToken);
                SelectionDecision selection = new SelectionParser().Parse(selectionOutput);
                return await ContinueAfterSelectionAsync(selection, projectContext, cancellationToken);
            }

            case RoadmapResumeAction.GenerateMilestoneSpecs:
                await generateMilestoneDeepDivesTransition.ExecuteAsync(projectContext, cancellationToken);
                return RoadmapOutcome.Paused;

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
                await ReportEphemeralBlockerAsync("Resume planning", resumePlan.Reason, resumePlan.SourceState);
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
        RoadmapCompletionRoute roadmapRoute = RoadmapCompletionRouteMapper.Map(route);
        string evaluationPath = unblockPlan.PrimaryEvidencePath
            ?? throw new RoadmapStepException("Completion certification unblock did not include an evaluation evidence path.");
        string reviewPath = await WriteUnblockReviewEvidenceAsync(persistedState, unblockPlan, success: true);
        await RecordUnblockJournalAsync(persistedState, unblockPlan, reviewPath, "Recovered", null);
        await decisionRecorder.AppendAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            "UnblockReview",
            persistedState.LastTransition.Projection,
            evaluationPath,
            certification.Decision.ClosureRecommendation,
            "Unclear",
            unblockPlan.Reason);

        CompletedEpicArchiveResult? archive = null;
        if (roadmapRoute.RequiresRoadmapCompletionContextUpdate)
        {
            ProjectContext projectContext = await projectContextLoader.LoadAsync(cancellationToken);
            archive = await completionArchive.ArchiveAndSynthesizeAsync(
                new CompletedEpicArchiveRequest(artifacts.Repository),
                cancellationToken);
            await UpdateRoadmapCompletionContextAsync(
                projectContext,
                evaluationPath,
                archive.SynthesisPath,
                archive.SynthesisContent,
                cancellationToken);
        }

        if (roadmapRoute.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                activeEpicLifecycleState,
                $"Completion certification unblock route: {roadmapRoute.ClosureRecommendation}");
        }

        await PersistCompletionRouteAsync(
            roadmapRoute,
            certification.Decision,
            persistedState.LastTransition.Projection,
            evaluationPath,
            archive is null ? [reviewPath] : [reviewPath, archive.SynthesisPath],
            []);
        return roadmapRoute.CliOutcome;
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

        await transitionPersistence.RefreshAndSaveAsync(persistedState with
        {
            Blockers = AppendUnblockReviewBlocker(
                persistedState.Blockers,
                unblockPlan,
                reviewPath),
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
            await bootstrapRoadmapCompletionContextTransition.ExecuteAsync(projectContext, cancellationToken);
        }

        return await RunSelectionAndFollowingAsync(projectContext, cancellationToken);
    }

    private async Task<RoadmapOutcome> RunSelectionAndFollowingAsync(
        ProjectContext projectContext,
        CancellationToken cancellationToken)
    {
        SelectionDecision selection = await selectNextEpicTransition.ExecuteAsync(projectContext, cancellationToken);
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
                EpicPreparationResult preparation = await epicPreparationAuditTransition.ExecuteAsync(selection, projectContext, cancellationToken);
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
                ArtifactPromotionResult createPromotion = await createNewEpicTransition.ExecuteAsync(projectContext, cancellationToken);
                if (!createPromotion.Promoted)
                {
                    return RoadmapOutcome.Paused;
                }

                break;
            case "Select Split Epic":
                ArtifactPromotionResult splitPromotion = await splitEpicTransition.ExecuteAsync(projectContext, cancellationToken);
                if (!splitPromotion.Promoted)
                {
                    return RoadmapOutcome.Paused;
                }

                break;
            case "Strategic Investigation Required":
                await decisionRecorder.AppendAsync(RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.StrategicInvestigationRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.StrategicInvestigationRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
            case "Roadmap Revision Required":
                await decisionRecorder.AppendAsync(RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.RoadmapRevisionRequired, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.RoadmapRevisionRequired, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
            default:
                await decisionRecorder.AppendAsync(RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, selection.Confidence, selection.PrimaryReason);
                await SaveStateAsync(RoadmapState.NoSuitableInitiative, TransitionStatus.Completed, RoadmapState.SelectNextStrategicInitiative, RoadmapState.NoSuitableInitiative, "SelectNextEpic", "SelectNextEpic", RoadmapArtifactPaths.Selection, selection.RecommendedOutcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
                return RoadmapOutcome.Paused;
        }

        await generateMilestoneDeepDivesTransition.ExecuteAsync(projectContext, cancellationToken);
        return RoadmapOutcome.Paused;
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
        if (nonImplementationCompletionReview is not null)
        {
            NonImplementationCompletionReviewResult review =
                await nonImplementationCompletionReview.ReviewAsync(cancellationToken);
            if (review.IsBlocked)
            {
                return await PersistNonImplementationCompletionReviewBlockedAsync(review);
            }
        }

        console.Phase("Evaluate epic completion and drift");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionEvaluationContextAsync(projection.Content, executionEvidencePath);
        string output = await promptTransitionRunner.RunNormalAsync(
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
        await hitlArtifactCapture.CaptureAsync(evaluationPath, output);
        CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(output);
        CompletionCertificationPolicyResult certification = completionPolicy.Validate(decision);
        await decisionRecorder.AppendAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, evaluationPath, decision.ClosureRecommendation, "Unclear", decision.OverallCompletionStatus);

        if (!certification.IsValid)
        {
            return await PersistInvalidCompletionCertificationAsync(
                certification,
                projection.Definition.ProjectionPath,
                evaluationPath);
        }

        CompletionCertificationRoute route = completionRouter.Route(certification.Decision);
        RoadmapCompletionRoute roadmapRoute = RoadmapCompletionRouteMapper.Map(route);
        CompletedEpicArchiveResult? archive = null;
        if (roadmapRoute.RequiresRoadmapCompletionContextUpdate)
        {
            archive = await completionArchive.ArchiveAndSynthesizeAsync(
                new CompletedEpicArchiveRequest(artifacts.Repository),
                cancellationToken);
            await UpdateRoadmapCompletionContextAsync(
                projectContext,
                evaluationPath,
                archive.SynthesisPath,
                archive.SynthesisContent,
                cancellationToken);
        }

        if (roadmapRoute.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                activeEpicLifecycleState,
                $"Completion certification route: {roadmapRoute.ClosureRecommendation}");
        }

        await PersistCompletionRouteAsync(
            roadmapRoute,
            decision,
            projection.Definition.ProjectionPath,
            evaluationPath,
            archive is null ? null : [archive.SynthesisPath]);
        return roadmapRoute.CliOutcome;
    }

    private async Task<RoadmapOutcome> PersistNonImplementationCompletionReviewBlockedAsync(
        NonImplementationCompletionReviewResult review)
    {
        DateTimeOffset blockedAt = DateTimeOffset.UtcNow;
        string nextStep =
            $"Fill `{LoopRelay.Orchestration.OrchestrationArtifactPaths.NonImplementationDecisions}` and rerun the roadmap CLI.";
        var detailsLines = new List<string>
        {
            "Non-implementation HITL review blocked completion evaluation.",
            string.Empty,
            "Review evidence paths:",
        };
        detailsLines.AddRange(review.EvidencePaths.Select(path => $"- {path}"));
        detailsLines.Add(string.Empty);
        detailsLines.Add("Blockers:");
        detailsLines.AddRange(review.BlockerMessages.Count == 0
            ? ["- Human review decisions are pending."]
            : review.BlockerMessages.Select(message => $"- {message}"));
        string details = string.Join(Environment.NewLine, detailsLines);
        string blockerPath = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "non-implementation-completion-review-blocked",
            RoadmapBlockedArtifact.Render(
                RoadmapState.EvidenceBlocked,
                "NonImplementationCompletionReview",
                "Pending non-implementation HITL review decisions.",
                nextStep,
                review.ReviewPath,
                details,
                blockedAt));
        IReadOnlyList<string> outputs = [..review.EvidencePaths, blockerPath];

        await SaveStateAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            RoadmapState.EpicCompletionDetected,
            RoadmapState.EvidenceBlocked,
            "NonImplementationCompletionReview",
            "None",
            FormatList(outputs),
            "Pending non-implementation HITL review",
            blockedAt,
            blockedAt,
            null,
            [new BlockerRow("Pending non-implementation HITL review decisions.", nextStep)],
            new RoadmapTransitionIntent("ResolveNonImplementationCompletionReview", RoadmapState.EvidenceBlocked, outputs),
            [nextStep]);
        return RoadmapOutcome.Paused;
    }

    private async Task<string> ReadPersistedExecutionEvidencePathAsync()
    {
        RoadmapStateDocument? state = await stateStore.LoadAsync();
        IReadOnlyList<string> candidates = (state?.TransitionIntent.EvidencePaths ?? [])
            .Concat(RoadmapTransitionPersistence.ParseOutputEvidencePaths(state?.LastTransition.Output ?? string.Empty))
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

    private async Task UpdateRoadmapCompletionContextAsync(
        ProjectContext projectContext,
        string evaluationPath,
        string completedEpicSynthesisPath,
        string completedEpicSynthesis,
        CancellationToken cancellationToken)
    {
        const string runtimePrompt = "UpdateRoadmapCompletionContext";
        console.Phase("Update roadmap completion context");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildCompletionUpdateContextAsync(
            projection.Content,
            evaluationPath,
            completedEpicSynthesisPath,
            completedEpicSynthesis);
        string output = await promptTransitionRunner.RunNormalAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            RoadmapState.SelectNextStrategicInitiative,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            completedEpicSynthesis,
            [RoadmapArtifactPaths.RoadmapCompletionContext],
            cancellationToken,
            TransitionInputContext.CompletionEvaluation(evaluationPath));
        await artifacts.WriteAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await hitlArtifactCapture.CaptureAsync(RoadmapArtifactPaths.RoadmapCompletionContext, output);
        await artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "roadmap-completion-update", output);
        await selectionSuperseder.SupersedeForRoadmapCompletionContextAsync();
        await decisionRecorder.AppendAsync(RoadmapState.CompletionEvaluationAndContextUpdate, runtimePrompt, projection.Definition.ProjectionPath, RoadmapArtifactPaths.RoadmapCompletionContext, "Roadmap Completion Context Updated", "Unclear", "Completion context updated after certification.");
    }

    private async Task PersistCompletionRouteAsync(
        RoadmapCompletionRoute route,
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
        await transitionPersistence.SaveAsync(
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
    }

    private static string ExecutionCommandText(ExecutionDispositionCommand command) =>
        ExecutionDispositionProtocol.CommandText(command);

    private async Task ReportEphemeralBlockerAsync(string source, string reason, RoadmapState? preservedState = null)
    {
        preservedState ??= (await stateStore.LoadAsync())?.CurrentState;
        string stateMessage = preservedState is null
            ? "No persisted roadmap state was written."
            : $"Persisted roadmap state remains {preservedState}.";
        console.Warn($"{source} blocked: {OneLine(reason)} {stateMessage} Fix the condition and rerun, or update state manually if this should be a durable blocker.");
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
            new RoadmapTransitionIntent("ResumeCancelledTransition", recoveryState, RoadmapTransitionPersistence.ParseOutputEvidencePaths(interrupted.Output)),
            ["Resume cancelled transition"]);
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

}
