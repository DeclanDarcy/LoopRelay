using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;
using CommandCenter.Backend.Execution;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryDashboardProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public RepositoryExecutionState ExecutionState { get; init; } = RepositoryExecutionState.Ready;

    public ExecutionSessionSummary? ActiveExecutionSession { get; init; }

    public int MilestoneCount { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }
}
