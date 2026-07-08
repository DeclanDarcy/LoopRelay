using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Projections;

namespace LoopRelay.Completion;

public sealed record CompletionCertificationResult(
    CompletionCertificationServiceOutcome Outcome,
    CompletionEvaluationDecision? Decision,
    CompletionCertificationRoute? Route,
    string? EvaluationEvidencePath,
    string? BlockerEvidencePath,
    IReadOnlyList<string> EvidencePaths,
    int? CompletedEpicArchiveIndex,
    string? CompletedEpicSynthesisPath,
    string? RoadmapCompletionUpdateEvidencePath,
    bool RoadmapCompletionContextChanged,
    string Message)
{
    public static CompletionCertificationResult Completed(
        CompletionEvaluationDecision decision,
        CompletionCertificationRoute route,
        string evaluationEvidencePath,
        IReadOnlyList<string> evidencePaths,
        int archiveIndex,
        string synthesisPath,
        string updateEvidencePath,
        string message) =>
        new(
            CompletionCertificationServiceOutcome.Completed,
            decision,
            route,
            evaluationEvidencePath,
            null,
            evidencePaths,
            archiveIndex,
            synthesisPath,
            updateEvidencePath,
            RoadmapCompletionContextChanged: true,
            message);

    public static CompletionCertificationResult Blocked(
        CompletionEvaluationDecision? decision,
        CompletionCertificationRoute? route,
        string? evaluationEvidencePath,
        string blockerEvidencePath,
        IReadOnlyList<string> evidencePaths,
        string message) =>
        new(
            CompletionCertificationServiceOutcome.Blocked,
            decision,
            route,
            evaluationEvidencePath,
            blockerEvidencePath,
            evidencePaths,
            null,
            null,
            null,
            RoadmapCompletionContextChanged: false,
            message);

    public static CompletionCertificationResult Failed(
        string? evaluationEvidencePath,
        string blockerEvidencePath,
        IReadOnlyList<string> evidencePaths,
        string message) =>
        new(
            CompletionCertificationServiceOutcome.Failed,
            null,
            null,
            evaluationEvidencePath,
            blockerEvidencePath,
            evidencePaths,
            null,
            null,
            null,
            RoadmapCompletionContextChanged: false,
            message);
}
