using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class GenerateMilestoneDeepDivesTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    ArtifactBundles.BundleFileExtractor bundleExtractor,
    BundleManifestWriter bundleManifestWriter,
    ExecutionPreparationProvenanceService executionPreparation,
    InvariantValidator invariantValidator,
    TransitionJournalStore journalStore,
    ArtifactLifecycleStore lifecycleStore,
    RoadmapTransitionPersistence transitionPersistence,
    HitlArtifactCapture hitlArtifactCapture,
    ILoopConsole console)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly PromptContractRegistry _contractRegistry = contractRegistry;
    private readonly ProjectionCache _projectionCache = projectionCache;
    private readonly RoadmapPromptContextBuilder _contextBuilder = contextBuilder;
    private readonly RoadmapPromptTransitionRunner _promptTransitionRunner = promptTransitionRunner;
    private readonly BundleFileExtractor _bundleExtractor = bundleExtractor;
    private readonly BundleManifestWriter _bundleManifestWriter = bundleManifestWriter;
    private readonly ExecutionPreparationProvenanceService _executionPreparation = executionPreparation;
    private readonly InvariantValidator _invariantValidator = invariantValidator;
    private readonly TransitionJournalStore _journalStore = journalStore;
    private readonly ArtifactLifecycleStore _lifecycleStore = lifecycleStore;
    private readonly RoadmapTransitionPersistence _transitionPersistence = transitionPersistence;
    private readonly HitlArtifactCapture _hitlArtifactCapture = hitlArtifactCapture;
    private readonly ILoopConsole _console = console;
    public async Task ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "GenerateMilestoneDeepDivesForEpic";
        _console.Phase("Generate milestone deep dives");
        PromptContract contract = _contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await _projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = await _contextBuilder.BuildMilestoneContextAsync(projection.Content);
        PromptTransitionCompletion completion = await _promptTransitionRunner.RunPromotionCandidateAsync(
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
            BundleExtractionResult bundle = _bundleExtractor.Extract(completion.Output);
            if (bundle.IsBlocked || bundle.Files.Count == 0)
            {
                throw new RoadmapStepException(bundle.BlockedReason ?? "Milestone deep dive output did not contain specs.");
            }

            await _bundleExtractor.WriteExtractedFilesAsync(_artifacts, bundle);
            await _bundleManifestWriter.WriteAsync($"{RoadmapArtifactPaths.SpecsDirectory}/bundle-manifest.md", runtimePrompt, projection.Definition.ProjectionPath, bundle, "Valid");
            foreach (ExtractedBundleFile file in bundle.Files)
            {
                await _lifecycleStore.UpsertAsync(file.Path, ArtifactLifecycleState.Ready);
                await _hitlArtifactCapture.CaptureAsync(file.Path, file.Content);
            }

            await _executionPreparation.RecordMilestoneSpecsAsync(
                bundle.Files.Select(file => file.Path).ToArray(),
                cancellationToken);

            InvariantValidationResult invariant = await _invariantValidator.ValidateAsync(RoadmapState.MilestoneSpecsReady, projectContext.Hash, cancellationToken);
            if (!invariant.IsValid)
            {
                await _transitionPersistence.PersistInvariantFailureAndThrowAsync(
                    invariant,
                    RoadmapState.ActiveEpicReady,
                    RoadmapState.MilestoneSpecsReady,
                    "PostMilestoneInvariantValidation",
                    projection.Definition.ProjectionPath);
            }

            DateTimeOffset completed = DateTimeOffset.UtcNow;
            await _journalStore.AppendAsync(new TransitionJournalRecord(
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
            await _transitionPersistence.SaveAsync(
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
        string evidencePath = await _artifacts.WriteNumberedEvidenceAsync(
            RoadmapArtifactPaths.BlockerEvidenceDirectory,
            "milestone-spec-generation-failed",
            RenderMilestoneSpecGenerationFailure(reason, completion.Output, failedAt));
        await _journalStore.AppendAsync(new TransitionJournalRecord(
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
        await _transitionPersistence.SaveAsync(
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
