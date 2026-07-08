using System.Collections.Concurrent;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Completion;
using LoopRelay.Projections;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Cli;

namespace LoopRelay.Cli.Tests;

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
