using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services.Transitions;

internal sealed class GenerateMilestoneDeepDivesTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    BundleFileExtractor bundleExtractor,
    BundleManifestWriter bundleManifestWriter,
    ExecutionPreparationProvenanceService executionPreparation,
    InvariantValidator invariantValidator,
    TransitionJournalStore journalStore,
    ArtifactLifecycleStore lifecycleStore,
    RoadmapTransitionPersistence transitionPersistence,
    HitlArtifactCapture hitlArtifactCapture,
    ILoopConsole console)
{
    public async Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "GenerateMilestoneDeepDivesForEpic";
        console.Phase("Generate milestone deep dives");
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await contextBuilder.BuildMilestoneContextAsync(projection.Content);
        PromptTransitionCompletion completion = await promptTransitionRunner.RunPromotionCandidateAsync(
            RoadmapState.ActiveEpicReady,
            RoadmapState.MilestoneSpecsReady,
            runtimePrompt,
            projection.Definition.ProjectionPath,
            context,
            string.Empty,
            [RoadmapArtifactPaths.SpecsDirectory],
            cancellationToken);

        try
        {
            BundleExtractionResult bundle = bundleExtractor.Extract(completion.Output);
            if (bundle.IsBlocked || bundle.Files.Count == 0)
            {
                throw new RoadmapStepException(bundle.BlockedReason ?? "Milestone deep dive output did not contain specs.");
            }

            await bundleExtractor.WriteExtractedFilesAsync(artifacts, bundle);
            await bundleManifestWriter.WriteAsync($"{RoadmapArtifactPaths.SpecsDirectory}/bundle-manifest.md", runtimePrompt, projection.Definition.ProjectionPath, bundle, "Valid");
            foreach (ExtractedBundleFile file in bundle.Files)
            {
                await lifecycleStore.UpsertAsync(file.Path, ArtifactLifecycleState.Ready);
                await hitlArtifactCapture.CaptureAsync(file.Path, file.Content);
            }

            await executionPreparation.RecordMilestoneSpecsAsync(
                bundle.Files.Select(file => file.Path).ToArray(),
                cancellationToken);

            InvariantValidationResult invariant = await invariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash, cancellationToken);
            if (!invariant.IsValid)
            {
                await transitionPersistence.PersistInvariantFailureAndThrowAsync(
                    invariant,
                    RoadmapState.ActiveEpicReady,
                    RoadmapState.MilestoneSpecsReady,
                    "PostMilestoneInvariantValidation",
                    projection.Definition.ProjectionPath);
            }

            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await journalStore.AppendAsync(new TransitionJournalRecord(
                "MilestoneSpecsMaterialized",
                completion.CorrelationId,
                completed,
                RoadmapState.ActiveEpicReady,
                RoadmapState.MilestoneSpecsReady,
                runtimePrompt,
                projection.Definition.ProjectionPath,
                "MilestoneSpecPostProcessing",
                completion.InputSnapshot.ToInputArtifactHashes(),
                [RoadmapArtifactPaths.SpecsDirectory],
                completion.ElapsedMilliseconds,
                "Completed",
                "Milestone Specs Ready",
                null,
                completion.InputSnapshot));
            await transitionPersistence.SaveAsync(
                RoadmapState.MilestoneSpecsReady,
                TransitionStatus.Completed,
                RoadmapState.ActiveEpicReady,
                RoadmapState.MilestoneSpecsReady,
                runtimePrompt,
                projection.Definition.ProjectionPath,
                RoadmapArtifactPaths.SpecsDirectory,
                "Milestone Specs Ready",
                completion.Started,
                completed,
                null,
                [],
                RoadmapTransitionIntent.Empty(RoadmapState.MilestoneSpecsReady));
        }
        catch (RoadmapStepException exception) when (exception.Persistence == RoadmapFailurePersistence.AlreadyPersisted)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await PersistMilestoneSpecGenerationFailureAndThrowAsync(
                completion,
                projection.Definition.ProjectionPath,
                exception.Message);
        }
    }

    private async Task PersistMilestoneSpecGenerationFailureAndThrowAsync(
        PromptTransitionCompletion completion,
        string projectionPath,
        string reason)
    {
        const string runtimePrompt = "GenerateMilestoneDeepDivesForEpic";
        DateTimeOffset failedAt = DateTimeOffset.UtcNow;
        string evidencePath = await artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "milestone-spec-generation-failed",
            RenderMilestoneSpecGenerationFailure(reason, completion.Output, failedAt));
        await journalStore.AppendAsync(new TransitionJournalRecord(
            "MilestoneSpecGenerationFailed",
            completion.CorrelationId,
            failedAt,
            RoadmapState.ActiveEpicReady,
            RoadmapState.MilestoneSpecsReady,
            runtimePrompt,
            projectionPath,
            "MilestoneSpecPostProcessing",
            completion.InputSnapshot.ToInputArtifactHashes(),
            [evidencePath],
            completion.ElapsedMilliseconds,
            TransitionStatus.Paused.ToString(),
            "Milestone Spec Generation Failed",
            reason,
            completion.InputSnapshot));
        await transitionPersistence.SaveAsync(
            RoadmapState.EvidenceBlocked,
            TransitionStatus.Paused,
            RoadmapState.ActiveEpicReady,
            RoadmapState.MilestoneSpecsReady,
            runtimePrompt,
            projectionPath,
            evidencePath,
            "Milestone Spec Generation Failed",
            completion.Started,
            failedAt,
            null,
            [new BlockerRow(OneLine(reason), $"Review {evidencePath}, repair milestone spec generation output, and rerun the roadmap CLI.")],
            new RoadmapTransitionIntent("ResolveMilestoneSpecGenerationFailure", RoadmapState.EvidenceBlocked, [evidencePath]),
            ["Resolve milestone spec generation failure and rerun"]);
        throw RoadmapStepException.AlreadyPersisted(new RoadmapStepException(reason));
    }

    private static string RenderMilestoneSpecGenerationFailure(
        string reason,
        string rawOutput,
        DateTimeOffset createdAt) =>
        $"""
        # Milestone Spec Generation Failed

        | Field | Value |
        |---|---|
        | Attempted State | {RoadmapState.MilestoneSpecsReady} |
        | Transition | GenerateMilestoneDeepDivesForEpic |
        | Reason | {reason} |
        | Required Output | {RoadmapArtifactPaths.SpecsDirectory} |
        | Created At | {createdAt:O} |

        ## Raw Prompt Output

        ```markdown
        {rawOutput}
        ```
        """;

    private static string OneLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
