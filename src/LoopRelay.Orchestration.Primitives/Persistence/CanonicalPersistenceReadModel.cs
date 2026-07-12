using LoopRelay.Core.Models.Repositories;

namespace LoopRelay.Orchestration.Persistence;

public sealed record CanonicalPersistenceReadModel(
    string ProjectionIdentity,
    CanonicalWorkflowPersistenceSnapshot Workflow,
    IReadOnlyList<CanonicalChainBoundaryEventRecord> ChainBoundaries)
{
    public static CanonicalPersistenceReadModel Empty { get; } = new(
        "canonical-persistence-read-model.v1",
        new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], []),
        []);
}

/// <summary>
/// Persistence-owned projection boundary. Consumers receive a stable read model and never query
/// ledger tables or choose storage implementations.
/// </summary>
public interface ICanonicalPersistenceProjection
{
    Task<CanonicalPersistenceReadModel> ProjectAsync(
        Repository repository,
        CancellationToken cancellationToken = default);
}

public sealed class CanonicalPersistenceProjection : ICanonicalPersistenceProjection
{
    public async Task<CanonicalPersistenceReadModel> ProjectAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        return new CanonicalPersistenceReadModel(
            "canonical-persistence-read-model.v1",
            await store.LoadSnapshotAsync(cancellationToken),
            await store.ReadChainBoundaryEventsAsync(cancellationToken));
    }
}
