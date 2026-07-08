using LoopRelay.Completion.Models;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletionCertificationService
{
    Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken = default);
}
