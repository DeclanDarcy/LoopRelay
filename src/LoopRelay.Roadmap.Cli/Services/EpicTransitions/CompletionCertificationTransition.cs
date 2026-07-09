using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Services.Certification;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Services;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.Execution;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Execution;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class CompletionCertificationTransition(
    RoadmapArtifacts _artifacts,
    Projections.ProjectContextLoader _projectContextLoader,
    PromptContractRegistry _contractRegistry,
    ProjectionCache _projectionCache,
    RoadmapPromptContextBuilder _contextBuilder,
    TransitionInputResolver _inputResolver,
    CompletionCertificationPolicy _completionPolicy,
    CompletionCertificationRouter _completionRouter,
    ICompletedEpicArchiveService _completionArchive,
    RoadmapTransitionPersistence _transitionPersistence,
    RoadmapPromptTransitionRunner _promptTransitionRunner,
    RoadmapCompletionContextUpdateTransition _completionContextUpdateTransition,
    DecisionRecorder _decisionRecorder,
    ITransitionJournalStore _journalStore,
    IArtifactLifecycleStore _lifecycleStore,
    HitlArtifactCapture _hitlArtifactCapture,
    ILoopConsole _console,
    INonImplementationCompletionReviewService? _nonImplementationCompletionReview = null)
{
    public async Task<RoadmapOutcome> ExecuteAsync(
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
            await _transitionPersistence.SaveAsync(
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
        if (_nonImplementationCompletionReview is not null)
        {
            NonImplementationCompletionReviewResult review =
                await _nonImplementationCompletionReview.ReviewAsync(cancellationToken);
            if (review.IsBlocked)
            {
                return await PersistNonImplementationCompletionReviewBlockedAsync(review);
            }
        }

        _console.Phase("Evaluate epic completion and drift");
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await _contextBuilder.BuildCompletionEvaluationContextAsync(projection.Content, executionEvidencePath);
        string output = await _promptTransitionRunner.RunNormalAsync(
            RoadmapState.EpicCompletionDetected,
            RoadmapState.CompletionEvaluationAndContextUpdate,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.EvaluationEvidenceDirectory],
            cancellationToken,
            TransitionInputContext.ExecutionEvidence(executionEvidencePath));
        string evaluationPath = await _artifacts.WriteNumberedEvidenceAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "epic-completion-and-drift", output);
        await _hitlArtifactCapture.CaptureAsync(evaluationPath, output);
        CompletionEvaluationDecision decision = new CompletionEvaluationParser().Parse(output);
        CompletionCertificationPolicyResult certification = _completionPolicy.Validate(decision);
        await _decisionRecorder.AppendAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            evaluationPath,
            decision.ClosureRecommendation,
            "Unclear",
            decision.OverallCompletionStatus);

        if (!certification.IsValid)
        {
            return await PersistInvalidCompletionCertificationAsync(
                certification,
                projection.Definition.ProjectionPath,
                evaluationPath);
        }

        CompletionCertificationRoute route = _completionRouter.Route(certification.Decision);
        RoadmapCompletionRoute roadmapRoute = RoadmapCompletionRouteMapper.Map(route);
        CompletedEpicArchiveResult? archive = await RunCloseRouteEffectsAsync(
            roadmapRoute,
            projectContext,
            evaluationPath,
            cancellationToken);

        if (roadmapRoute.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await _lifecycleStore.UpsertAsync(
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

    public async Task<RoadmapOutcome> RecoverAsync(
        CompletionCertificationPolicyResult certification,
        CompletionCertificationRoute route,
        string projectionPath,
        string evaluationPath,
        string reviewPath,
        string recoveryReason,
        CancellationToken cancellationToken)
    {
        RoadmapCompletionRoute roadmapRoute = RoadmapCompletionRouteMapper.Map(route);
        await _decisionRecorder.AppendAsync(
            RoadmapState.CompletionEvaluationAndContextUpdate,
            "UnblockReview",
            projectionPath,
            evaluationPath,
            certification.Decision.ClosureRecommendation,
            "Unclear",
            recoveryReason);

        CompletedEpicArchiveResult? archive = null;
        if (roadmapRoute.RequiresRoadmapCompletionContextUpdate)
        {
            ProjectContext projectContext = await _projectContextLoader.LoadAsync(cancellationToken);
            archive = await RunCloseRouteEffectsAsync(
                roadmapRoute,
                projectContext,
                evaluationPath,
                cancellationToken);
        }

        if (roadmapRoute.ActiveEpicLifecycleState is { } activeEpicLifecycleState)
        {
            await _lifecycleStore.UpsertAsync(
                RoadmapArtifactPaths.ActiveEpic,
                activeEpicLifecycleState,
                $"Completion certification unblock route: {roadmapRoute.ClosureRecommendation}");
        }

        await PersistCompletionRouteAsync(
            roadmapRoute,
            certification.Decision,
            projectionPath,
            evaluationPath,
            archive is null ? [reviewPath] : [reviewPath, archive.SynthesisPath],
            []);
        return roadmapRoute.CliOutcome;
    }

    private async Task<CompletedEpicArchiveResult?> RunCloseRouteEffectsAsync(
        RoadmapCompletionRoute route,
        ProjectContext projectContext,
        string evaluationPath,
        CancellationToken cancellationToken)
    {
        if (!route.RequiresRoadmapCompletionContextUpdate)
        {
            return null;
        }

        CompletedEpicArchiveResult archive = await _completionArchive.ArchiveAndSynthesizeAsync(
            new CompletedEpicArchiveRequest(_artifacts.Repository),
            cancellationToken);
        await _completionContextUpdateTransition.ExecuteAsync(
            projectContext,
            evaluationPath,
            archive.SynthesisPath,
            archive.SynthesisContent,
            cancellationToken);
        return archive;
    }

    private async Task<RoadmapOutcome> PersistNonImplementationCompletionReviewBlockedAsync(
        NonImplementationCompletionReviewResult review)
    {
        DateTimeOffset blockedAt = DateTimeOffset.UtcNow;
        string nextStep =
            $"Fill `{OrchestrationArtifactPaths.NonImplementationDecisions}` and rerun the roadmap CLI.";
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
        string blockerPath = await _artifacts.WriteNumberedEvidenceAsync(
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

        await _transitionPersistence.SaveAsync(
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
        TransitionInputSnapshot inputSnapshot = await _inputResolver.ResolveAsync(new TransitionInputRequest(
            "CompletionCertificationRouting",
            projectionPath,
            routingContext,
            string.Empty,
            TransitionInputContext.CompletionEvaluation(evaluationPath)));
        await _journalStore.AppendAsync(new TransitionJournalRecord(
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
        await _transitionPersistence.SaveAsync(
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
        string blockerPath = await _artifacts.WriteNumberedEvidenceAsync(
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
        TransitionInputSnapshot inputSnapshot = await _inputResolver.ResolveAsync(new TransitionInputRequest(
            "CompletionCertificationRouting",
            projectionPath,
            routingContext,
            string.Empty,
            TransitionInputContext.CompletionEvaluation(evaluationPath)));
        IReadOnlyList<string> outputs = [evaluationPath, blockerPath];
        await _journalStore.AppendAsync(new TransitionJournalRecord(
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
        await _transitionPersistence.SaveAsync(
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

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0 ? "None" : string.Join(", ", values);

    private static string OneLine(string value) =>
        string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
