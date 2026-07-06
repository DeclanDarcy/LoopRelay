using LoopRelay.Core.Repositories;

namespace LoopRelay.Orchestration.Abstractions;

/// <summary>
/// Commits and pushes the planning + milestone artifacts as Execute Plan crosses from authoring into
/// execution (m4). It is the orchestration composition root's thin adapter over the Execution layer's
/// Git service, so the orchestrator depends on this focused two-operation seam rather than the broader,
/// execution-session-coupled <c>IGitService</c> — and unit tests can fake publication without a real repo.
/// </summary>
public interface IPlanArtifactPublisher
{
    /// <summary>
    /// Stages the given repository-relative paths, commits them with <paramref name="commitMessage"/>,
    /// and pushes. Never throws for an expected Git failure — it returns a failed
    /// <see cref="PlanPublicationResult"/> so the orchestrator can surface a clean terminal stream event
    /// and remain re-runnable.
    /// </summary>
    Task<PlanPublicationResult> PublishAsync(
        Repository repository,
        string commitMessage,
        IReadOnlyList<string> repositoryRelativePaths,
        CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a commit+push. <see cref="FailureReason"/> is null on success.</summary>
public sealed record PlanPublicationResult(bool Pushed, string? CommitSha, string? FailureReason)
{
    public bool Succeeded => FailureReason is null;

    public static PlanPublicationResult Success(string? commitSha, bool pushed) => new(pushed, commitSha, null);

    public static PlanPublicationResult Failed(string reason) => new(false, null, reason);
}
