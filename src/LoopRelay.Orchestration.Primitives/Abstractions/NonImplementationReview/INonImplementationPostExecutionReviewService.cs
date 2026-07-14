using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.RepositorySlices;

namespace LoopRelay.Orchestration.Abstractions.NonImplementationReview;

public interface INonImplementationPostExecutionReviewService
{
    Task<RepositorySliceBaseline> CapturePreSliceBaselineAsync(CancellationToken cancellationToken = default);

    Task<NonImplementationPostExecutionReviewResult> ReviewAfterExecutionAsync(
        RepositorySliceBaseline baseline,
        CancellationToken cancellationToken = default);
}
