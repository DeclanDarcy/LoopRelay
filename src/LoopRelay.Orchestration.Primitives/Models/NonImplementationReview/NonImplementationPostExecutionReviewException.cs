using System.Globalization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationPostExecutionReviewException : InvalidOperationException
{
    public NonImplementationPostExecutionReviewException(
        string message,
        IReadOnlyList<string> evidencePaths,
        Exception? innerException = null)
        : base(message, innerException)
    {
        EvidencePaths = evidencePaths;
    }

    public IReadOnlyList<string> EvidencePaths { get; }
}
