using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Abstractions.NonImplementationReview;

public interface INonImplementationCompletionReviewService
{
    Task<NonImplementationCompletionReviewResult> ReviewAsync(
        CancellationToken cancellationToken = default);
}
