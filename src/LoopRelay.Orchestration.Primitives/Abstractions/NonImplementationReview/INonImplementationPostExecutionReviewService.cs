using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public interface INonImplementationPostExecutionReviewService
{
    Task<RepositorySliceBaseline> CapturePreSliceBaselineAsync(CancellationToken cancellationToken = default);

    Task<NonImplementationPostExecutionReviewResult> ReviewAfterExecutionAsync(
        RepositorySliceBaseline baseline,
        CancellationToken cancellationToken = default);
}
