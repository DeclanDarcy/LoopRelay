using CommandCenter.Core.Continuity;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Middle.Projections;

public sealed class RepositoryWorkspaceProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public RepositoryExecutionState ExecutionState { get; init; } = RepositoryExecutionState.Ready;

    public ExecutionSessionSummary? ExecutionSummary { get; init; }

    public IReadOnlyList<ExecutionSessionSummary> ExecutionHistory { get; init; } = [];

    public ArtifactInventory ArtifactInventory { get; init; } = new();

    public int MilestoneCount { get; init; }

    public bool HasPlan { get; init; }

    public bool HasOperationalContext { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }

    public OperationalContextProposalSummary OperationalContextProposalSummary { get; init; } = new();

    public OperationalContextProjection OperationalContext { get; init; } = new();
}
