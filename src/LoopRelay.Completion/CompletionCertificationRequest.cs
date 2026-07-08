using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Projections;

namespace LoopRelay.Completion;

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
