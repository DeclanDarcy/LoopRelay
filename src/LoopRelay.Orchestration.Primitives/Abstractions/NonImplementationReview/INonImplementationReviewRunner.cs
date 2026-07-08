using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Orchestration.Abstractions.NonImplementationReview;

public interface INonImplementationReviewRunner
{
    NonImplementationReviewRunnerConstraints Capabilities => NonImplementationReviewRunnerConstraints.ReadOnly;

    Task<NonImplementationReviewRunnerResponse> RunAsync(
        NonImplementationReviewRunnerRequest request,
        CancellationToken cancellationToken);
}
