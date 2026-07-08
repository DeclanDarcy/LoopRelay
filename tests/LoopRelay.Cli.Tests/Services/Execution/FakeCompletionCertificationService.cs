using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models;
using LoopRelay.Completion.Primitives;

namespace LoopRelay.Cli.Tests.Services;

internal sealed class FakeCompletionCertificationService : ICompletionCertificationService
{
    public CompletionCertificationResult Result { get; set; } = CompletionCertificationResult.Completed(
        new CompletionEvaluationDecision("Fully Complete", "None", "Close Epic"),
        new CompletionCertificationRoute(
            "Close Epic",
            CompletionTransitionIntent.UpdateRoadmapCompletionContext,
            RequiresRoadmapCompletionContextUpdate: true,
            ShouldCloseEpic: true),
        ".agents/evidence/evaluations/epic-completion-and-drift.0001.md",
        [".agents/evidence/execution/main-cli-completion-claim.0001.md"],
        1,
        ".agents/archive/epics/1.md",
        ".agents/evidence/evaluations/roadmap-completion-update.0001.md",
        "Completion certified.");

    public List<CompletionCertificationRequest> Requests { get; } = new();

    public Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(Result);
    }
}
