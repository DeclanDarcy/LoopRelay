namespace LoopRelay.Orchestration.Abstractions.NonImplementationReview;

public interface INonImplementationReviewRunner
{
    Task<NonImplementationReviewRunnerResponse> RunAsync(
        NonImplementationReviewRunnerRequest request,
        CancellationToken cancellationToken);
}

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

public sealed class NonImplementationReviewRunnerConstraints
{
    private NonImplementationReviewRunnerConstraints(
        bool allowsWorkspaceWrites,
        bool allowsCommits,
        bool allowsPushes,
        bool allowsMutationCapableScopedOperations)
    {
        AllowsWorkspaceWrites = allowsWorkspaceWrites;
        AllowsCommits = allowsCommits;
        AllowsPushes = allowsPushes;
        AllowsMutationCapableScopedOperations = allowsMutationCapableScopedOperations;
    }

    public static NonImplementationReviewRunnerConstraints ReadOnly { get; } =
        new(
            allowsWorkspaceWrites: false,
            allowsCommits: false,
            allowsPushes: false,
            allowsMutationCapableScopedOperations: false);

    public bool AllowsWorkspaceWrites { get; }

    public bool AllowsCommits { get; }

    public bool AllowsPushes { get; }

    public bool AllowsMutationCapableScopedOperations { get; }

    public void EnsureReadOnly()
    {
        if (AllowsWorkspaceWrites || AllowsCommits || AllowsPushes || AllowsMutationCapableScopedOperations)
        {
            throw new InvalidOperationException("Non-implementation review runners must be read-only.");
        }
    }
}
