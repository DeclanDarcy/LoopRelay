using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Invocation;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.EpicTransitions;

internal sealed class SplitEpicTransition(
    RoadmapArtifacts artifacts,
    PromptContractRegistry contractRegistry,
    ProjectionCache projectionCache,
    RoadmapPromptContextBuilder contextBuilder,
    ActiveSelectionReader activeSelectionReader,
    RoadmapPromptTransitionRunner promptTransitionRunner,
    ActiveEpicPromotionCoordinator activeEpicPromotionCoordinator,
    ArtifactBundles.BundleFileExtractor bundleExtractor,
    Splits.SplitEpicBundleInterpreter splitBundleInterpreter,
    BundleManifestWriter bundleManifestWriter,
    SplitFamilyStore splitFamilyStore,
    ArtifactLifecycleStore lifecycleStore,
    TransitionJournalStore journalStore,
    RoadmapTransitionPersistence transitionPersistence,
    HitlArtifactCapture hitlArtifactCapture,
    ILoopConsole console)
{
    public async Task<ArtifactPromotionResult> ExecuteAsync(ProjectContext projectContext, CancellationToken cancellationToken)
    {
        const string runtimePrompt = "SplitEpic";
        console.Phase("Split epic");
        string selection = await activeSelectionReader.ReadAsync(cancellationToken);
        PromptContract contract = contractRegistry.Get(runtimePrompt);
        ProjectionCacheResult projection = await projectionCache.EnsureAsync(runtimePrompt, projectContext, contract, cancellationToken);
        string context = contextBuilder.BuildCreateOrSplitContext(projection.Content, selection);
        PromptTransitionCompletion completion = await promptTransitionRunner.RunNormalWithCompletionAsync(
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
            await hitlArtifactCapture.CaptureAsync(child.Path, child.Content);
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
        return await activeEpicPromotionCoordinator.PromoteAsync(
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
        await transitionPersistence.SaveAsync(
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
}
