using LoopRelay.Completion.Models;
using LoopRelay.Completion.Models.Certification;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletionCertificationService
{
    Task<CompletionCertificationResult> CertifyPlanCompletionAsync(
        CompletionCertificationRequest request,
        CancellationToken cancellationToken = default);
}
