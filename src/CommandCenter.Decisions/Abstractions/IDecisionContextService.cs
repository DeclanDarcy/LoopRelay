using CommandCenter.Decisions.Models;

namespace CommandCenter.Decisions.Abstractions;

public interface IDecisionContextService
{
    Task<DecisionContext> BuildContextAsync(Guid repositoryId);

    Task<DecisionContextSnapshot> CreateSnapshotAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionContextSnapshot>> ListSnapshotsAsync(Guid repositoryId);
}
