using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Abstractions.NonImplementationReview;

public interface INonImplementationPostExecutionReviewService
{
    Task<RepositorySliceBaseline> CapturePreSliceBaselineAsync(CancellationToken cancellationToken = default);

    Task<NonImplementationPostExecutionReviewResult> ReviewAfterExecutionAsync(
        RepositorySliceBaseline baseline,
        CancellationToken cancellationToken = default);
}
