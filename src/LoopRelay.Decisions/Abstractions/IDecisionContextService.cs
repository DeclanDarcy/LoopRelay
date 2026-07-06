using LoopRelay.Decisions.Models;

namespace LoopRelay.Decisions.Abstractions;

public interface IDecisionContextService
{
    Task<DecisionContext> BuildContextAsync(Guid repositoryId);

    Task<DecisionContextSnapshot> CreateSnapshotAsync(Guid repositoryId);

    Task<IReadOnlyList<DecisionContextSnapshot>> ListSnapshotsAsync(Guid repositoryId);
}
