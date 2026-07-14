namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationReviewRunnerConstraints
{
    public NonImplementationReviewRunnerConstraints(
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
