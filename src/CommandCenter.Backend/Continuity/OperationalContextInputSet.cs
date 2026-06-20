using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextInputSet
{
    public Repository Repository { get; init; } = new();

    public string? CurrentOperationalContext { get; init; }

    public string? CurrentHandoff { get; init; }

    public string? CurrentDecisions { get; init; }

    public IReadOnlyList<DecisionArtifactInput> DecisionArtifacts { get; init; } = [];

    public IReadOnlyList<ExecutionSessionSummary> ExecutionHistory { get; init; } = [];

    public IReadOnlyList<string> MilestonePaths { get; init; } = [];

    public bool HasPlan { get; init; }

    public string PlanningReadiness { get; init; } = string.Empty;
}
