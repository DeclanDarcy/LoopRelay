namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class NonImplementationReviewRunnerResponse
{
    public NonImplementationReviewRunnerResponse(string structuredText)
    {
        if (string.IsNullOrWhiteSpace(structuredText))
        {
            throw new ArgumentException("Structured text must not be empty.", nameof(structuredText));
        }

        StructuredText = structuredText;
    }

    public string StructuredText { get; }
}
