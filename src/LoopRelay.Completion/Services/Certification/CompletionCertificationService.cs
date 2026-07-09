using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Archive;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Completion.Models.Parsing;
using LoopRelay.Completion.Models.Prompts;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Completion.Services.Observers;
using LoopRelay.Completion.Services.Prompts;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Orchestration.Services;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;

namespace LoopRelay.Completion.Services.Certification;

public sealed class CompletionCertificationService(
    IArtifactStore _store,
    IProjectContextProjectionService _projectionService,
    ICompletionPromptRunner _promptRunner,
    ICompletedEpicArchiveService _archiveService,
    CompletionCertificationPolicy? _policy = null,
    CompletionCertificationRouter? _router = null,
    ICompletionObserver? _observer = null,
    IExecutionEvidenceStore? _executionEvidenceStore = null) : ICompletionCertificationService
{

    public async Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var artifacts = new ArtifactStorage.CompletionArtifacts(_store, request.Repository, _executionEvidenceStore);
        var contextBuilder = new CompletionPromptContextBuilder(artifacts);
        string? evaluationPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            (_observer ?? NullCompletionObserver.Instance).Phase("Completion Certification");
            if (string.IsNullOrWhiteSpace(await artifacts.ReadAsync(request.ActiveEpicPath)))
            {
                return await BlockAsync(
                    artifacts,
                    null,
                    null,
                    null,
                    "Missing active epic",
                    $"Main CLI cannot certify roadmap completion because `{request.ActiveEpicPath}` is missing or empty.");
            }

            IReadOnlyList<string> milestonePaths = await artifacts.ListAsync(
                request.MilestoneDirectory,
                CompletionArtifactPaths.MilestoneSearchPattern);
            if (milestonePaths.Count == 0)
            {
                return await BlockAsync(
                    artifacts,
                    null,
                    null,
                    null,
                    "No milestone evidence",
                    $"Main CLI completion certification requires at least one milestone file under `{request.MilestoneDirectory}`.");
            }

            if (string.IsNullOrWhiteSpace(await artifacts.ReadAsync(request.RoadmapCompletionContextPath)))
            {
                await BootstrapRoadmapCompletionContextAsync(artifacts, contextBuilder, request, cancellationToken);
            }

            string claimPath = await WriteExecutionCompletionClaimAsync(artifacts, request, milestonePaths);

            (_observer ?? NullCompletionObserver.Instance).Phase("Evaluate epic completion and drift");
            ProjectContextProjectionResult evaluationProjection = await _projectionService.EnsureFreshAsync(
                CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift,
                cancellationToken);
            string evaluationContext = await contextBuilder.BuildEvaluationContextAsync(
                request,
                evaluationProjection.Content,
                claimPath);
            string evaluationOutput = await _promptRunner.RunAsync(
                new CompletionRuntimePromptInvocation(
                    CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift,
                    ProjectContext: evaluationContext),
                cancellationToken);
            evaluationPath = await artifacts.WriteNumberedEvidenceAsync(
                CompletionArtifactPaths.EvaluationEvidenceDirectory,
                "epic-completion-and-drift",
                evaluationOutput);

            CompletionEvaluationDecision decision;
            try
            {
                decision = new CompletionEvaluationParser().Parse(evaluationOutput);
            }
            catch (CompletionMarkdownParseException exception)
            {
                return await BlockAsync(
                    artifacts,
                    null,
                    null,
                    evaluationPath,
                    "Malformed completion evaluation",
                    exception.Message,
                    [claimPath, evaluationPath]);
            }

            CompletionCertificationPolicyResult certification = (_policy ?? new CompletionCertificationPolicy()).Validate(decision);
            if (!certification.IsValid)
            {
                return await BlockAsync(
                    artifacts,
                    decision,
                    null,
                    evaluationPath,
                    "Invalid completion certification",
                    certification.RejectionReason ?? "Completion certification failed semantic policy validation.",
                    [claimPath, evaluationPath]);
            }

            CompletionCertificationRoute route = (_router ?? new CompletionCertificationRouter()).Route(decision);
            if (!route.ShouldCloseEpic)
            {
                return await BlockAsync(
                    artifacts,
                    decision,
                    route,
                    evaluationPath,
                    "Completion certification did not close the epic",
                    $"Certification recommended `{decision.ClosureRecommendation}`. The milestone checkbox gate is exhausted, so Main CLI cannot continue without operator intervention.",
                    [claimPath, evaluationPath]);
            }

            CompletedEpicArchiveResult archive = await _archiveService.ArchiveAndSynthesizeAsync(
                new CompletedEpicArchiveRequest(
                    request.Repository,
                    request.ActiveEpicPath,
                    request.CompletedEpicArchiveRoot),
                cancellationToken);

            (_observer ?? NullCompletionObserver.Instance).Phase("Update roadmap completion context");
            ProjectContextProjectionResult updateProjection = await _projectionService.EnsureFreshAsync(
                CompletionRuntimePromptNames.UpdateRoadmapCompletionContext,
                cancellationToken);
            string updateContext = await contextBuilder.BuildCompletionUpdateContextAsync(
                updateProjection.Content,
                request.RoadmapCompletionContextPath,
                archive.SynthesisPath,
                archive.SynthesisContent,
                evaluationPath,
                ArchivedNonImplementationReviewEvidencePaths(
                    archive.ArchiveDirectory,
                    request.NonImplementationReviewEvidencePaths));
            string updatedRoadmapCompletionContext = await _promptRunner.RunAsync(
                new CompletionRuntimePromptInvocation(
                    CompletionRuntimePromptNames.UpdateRoadmapCompletionContext,
                    ProjectContext: updateContext,
                    SecondaryInput: archive.SynthesisContent),
                cancellationToken);
            await artifacts.WriteAsync(request.RoadmapCompletionContextPath, updatedRoadmapCompletionContext);
            string updateEvidencePath = await artifacts.WriteNumberedEvidenceAsync(
                CompletionArtifactPaths.EvaluationEvidenceDirectory,
                "roadmap-completion-update",
                updatedRoadmapCompletionContext);

            return CompletionCertificationResult.Completed(
                decision,
                route,
                evaluationPath,
                [claimPath, evaluationPath, archive.SynthesisPath, updateEvidencePath],
                archive.Index,
                archive.SynthesisPath,
                updateEvidencePath,
                $"Completion certified as `{decision.ClosureRecommendation}`.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            string blocker = await WriteBlockerEvidenceAsync(
                artifacts,
                "Completion certification failed",
                exception.Message,
                evaluationPath,
                null,
                null);
            return CompletionCertificationResult.Failed(
                evaluationPath,
                blocker,
                evaluationPath is null ? [blocker] : [evaluationPath, blocker],
                exception.Message);
        }
    }

    private async Task BootstrapRoadmapCompletionContextAsync(
        ArtifactStorage.CompletionArtifacts artifacts,
        CompletionPromptContextBuilder contextBuilder,
        CompletionCertificationRequest request,
        CancellationToken cancellationToken)
    {
        (_observer ?? NullCompletionObserver.Instance).Phase("Bootstrap roadmap completion context");
        ProjectContextProjectionResult projection = await _projectionService.EnsureFreshAsync(
            CompletionRuntimePromptNames.CreateRoadmapCompletionContext,
            cancellationToken);
        string completedEpicEvidence = await new CompletedEpicEvidenceLoader(artifacts).RenderAsync();
        string context = contextBuilder.BuildRoadmapCompletionBootstrapContext(projection.Content);
        string output = await _promptRunner.RunAsync(
            new CompletionRuntimePromptInvocation(
                CompletionRuntimePromptNames.CreateRoadmapCompletionContext,
                ProjectContext: context,
                SecondaryInput: completedEpicEvidence),
            cancellationToken);
        await artifacts.WriteAsync(request.RoadmapCompletionContextPath, output);
        await artifacts.WriteNumberedEvidenceAsync(
            CompletionArtifactPaths.EvaluationEvidenceDirectory,
            "roadmap-completion-bootstrap",
            output);
    }

    private static async Task<string> WriteExecutionCompletionClaimAsync(
        ArtifactStorage.CompletionArtifacts artifacts,
        CompletionCertificationRequest request,
        IReadOnlyList<string> milestonePaths)
    {
        string content = $"""
            # Main CLI Completion Claim

            | Field | Value |
            |---|---|
            | Trigger | {Escape(request.CompletionTrigger)} |
            | Active Epic Path | {Escape(request.ActiveEpicPath)} |
            | Execution Plan Path | {Escape(request.ExecutionPlanPath)} |
            | Milestone Directory | {Escape(request.MilestoneDirectory)} |
            | Milestone Files | {Escape(string.Join(", ", milestonePaths.Order(StringComparer.Ordinal)))} |
            | Created At | {DateTimeOffset.UtcNow:O} |

            Main CLI reached the milestone-complete gate because every strict checkbox in the executed milestone files was checked.
            This artifact is a completion claim and must be certified against repository reality before the epic can close.
            """;
        return await artifacts.WriteNumberedEvidenceAsync(
            CompletionArtifactPaths.ExecutionEvidenceDirectory,
            "main-cli-completion-claim",
            content);
    }

    private static IReadOnlyList<string> ArchivedNonImplementationReviewEvidencePaths(
        string archiveDirectory,
        IReadOnlyList<string>? requestedEvidencePaths)
    {
        IReadOnlyList<string> paths = requestedEvidencePaths is { Count: > 0 }
            ? requestedEvidencePaths
            :
            [
                OrchestrationArtifactPaths.NonImplementationReview,
                OrchestrationArtifactPaths.NonImplementationSynthesis,
            ];

        return paths
            .Select(path => ArchivedNonImplementationReviewEvidencePath(archiveDirectory, path))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string ArchivedNonImplementationReviewEvidencePath(
        string archiveDirectory,
        string path)
    {
        string reviewDirectory = OrchestrationArtifactPaths.NonImplementationReviewDirectory + "/";
        if (!path.StartsWith(reviewDirectory, StringComparison.Ordinal))
        {
            return path;
        }

        return $"{archiveDirectory}/review/{path[reviewDirectory.Length..]}";
    }

    private static async Task<CompletionCertificationResult> BlockAsync(
        ArtifactStorage.CompletionArtifacts artifacts,
        CompletionEvaluationDecision? decision,
        CompletionCertificationRoute? route,
        string? evaluationPath,
        string title,
        string reason,
        IReadOnlyList<string>? priorEvidence = null)
    {
        string blocker = await WriteBlockerEvidenceAsync(
            artifacts,
            title,
            reason,
            evaluationPath,
            decision,
            route);
        IReadOnlyList<string> evidence = priorEvidence is null
            ? [blocker]
            : [..priorEvidence, blocker];
        return CompletionCertificationResult.Blocked(
            decision,
            route,
            evaluationPath,
            blocker,
            evidence,
            reason);
    }

    private static async Task<string> WriteBlockerEvidenceAsync(
        ArtifactStorage.CompletionArtifacts artifacts,
        string title,
        string reason,
        string? evaluationPath,
        CompletionEvaluationDecision? decision,
        CompletionCertificationRoute? route)
    {
        string content = $"""
            # {title}

            | Field | Value |
            |---|---|
            | Reason | {Escape(reason)} |
            | Evaluation Evidence | {Escape(evaluationPath ?? "None")} |
            | Closure Recommendation | {Escape(decision?.ClosureRecommendation ?? "None")} |
            | Completion Status | {Escape(decision?.OverallCompletionStatus ?? "None")} |
            | Drift Classification | {Escape(decision?.OverallDriftClassification ?? "None")} |
            | Route Intent | {Escape(route?.Intent.ToString() ?? "None")} |
            | Created At | {DateTimeOffset.UtcNow:O} |

            Completion certification did not reach a close-worthy state. Main CLI must not report `EpicCompleted`
            until certification closes the epic and roadmap completion context is updated.
            """;
        return await artifacts.WriteNumberedEvidenceAsync(
            CompletionArtifactPaths.BlockerEvidenceDirectory,
            "completion-certification-blocked",
            content);
    }

    private static string Escape(string value) =>
        value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
