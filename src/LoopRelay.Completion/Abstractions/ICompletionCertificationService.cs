using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Projections;

namespace LoopRelay.Completion;

public interface ICompletionCertificationService
{
    Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken = default);
}
