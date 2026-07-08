using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Completion.Models.Certification;

public sealed record CompletionCertificationRequest(
    Repository Repository,
    string ActiveEpicPath = CompletionArtifactPaths.ActiveEpic,
    string RoadmapCompletionContextPath = CompletionArtifactPaths.RoadmapCompletionContext,
    string ExecutionPlanPath = CompletionArtifactPaths.ExecutionPlan,
    string? DetailsPath = CompletionArtifactPaths.Details,
    string MilestoneDirectory = CompletionArtifactPaths.MilestonesDirectory,
    string CompletionTrigger = "MainCliMilestoneGate",
    string CompletedEpicArchiveRoot = CompletionArtifactPaths.CompletedEpicsDirectory,
    IReadOnlyList<string>? NonImplementationReviewEvidencePaths = null);
