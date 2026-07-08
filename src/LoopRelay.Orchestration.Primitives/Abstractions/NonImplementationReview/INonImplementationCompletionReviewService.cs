using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public interface INonImplementationCompletionReviewService
{
    Task<NonImplementationCompletionReviewResult> ReviewAsync(
        CancellationToken cancellationToken = default);
}
