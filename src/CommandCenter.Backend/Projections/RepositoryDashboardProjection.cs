using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryDashboardProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public int MilestoneCount { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }
}
