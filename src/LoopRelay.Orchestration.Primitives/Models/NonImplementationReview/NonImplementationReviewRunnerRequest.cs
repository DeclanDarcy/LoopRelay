using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed class NonImplementationReviewRunnerRequest
{
    public NonImplementationReviewRunnerRequest(
        string promptName,
        string promptPayload,
        int maxPromptPayloadCharacters)
    {
        ArgumentNullException.ThrowIfNull(promptPayload);

        if (string.IsNullOrWhiteSpace(promptName))
        {
            throw new ArgumentException("Prompt name must not be empty.", nameof(promptName));
        }

        if (maxPromptPayloadCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxPromptPayloadCharacters),
                "Maximum prompt payload characters must be positive.");
        }

        if (promptPayload.Length > maxPromptPayloadCharacters)
        {
            throw new ArgumentException(
                "Prompt payload exceeds the configured review-runner bound.",
                nameof(promptPayload));
        }

        PromptName = promptName.Trim();
        PromptPayload = promptPayload;
        MaxPromptPayloadCharacters = maxPromptPayloadCharacters;
        Constraints = NonImplementationReviewRunnerConstraints.ReadOnly;
    }

    public string PromptName { get; }

    public string PromptPayload { get; }

    public int MaxPromptPayloadCharacters { get; }

    public NonImplementationReviewRunnerConstraints Constraints { get; }
}
