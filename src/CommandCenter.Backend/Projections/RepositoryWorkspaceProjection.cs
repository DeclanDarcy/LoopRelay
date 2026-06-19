using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryWorkspaceProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public ArtifactInventory ArtifactInventory { get; init; } = new();

    public int MilestoneCount { get; init; }

    public bool HasPlan { get; init; }

    public bool HasOperationalContext { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }
}
