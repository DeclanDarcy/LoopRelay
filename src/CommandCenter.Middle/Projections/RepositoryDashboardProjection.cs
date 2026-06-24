using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Middle.Projections;

public sealed class RepositoryDashboardProjection
{
    public Repository Repository { get; init; } = new();

    public RepositoryAvailability Availability { get; init; }

    public ExecutionReadiness Readiness { get; init; }

    public RepositoryExecutionState ExecutionState { get; init; } = RepositoryExecutionState.Ready;

    public ExecutionSessionSummary? ActiveExecutionSession { get; init; }

    public ExecutionSessionSummary? ExecutionSummary { get; init; }

    public IReadOnlyList<ExecutionSessionSummary> ExecutionHistory { get; init; } = [];

    public int MilestoneCount { get; init; }

    public bool HasCurrentHandoff { get; init; }

    public bool HasCurrentDecisions { get; init; }

    public RepositoryContinuitySummary ContinuitySummary { get; init; } = new();

    public RepositoryReasoningSummary ReasoningSummary { get; init; } = new();

    public RepositoryDecisionSessionSummary DecisionSessionSummary { get; init; } = new();
}
