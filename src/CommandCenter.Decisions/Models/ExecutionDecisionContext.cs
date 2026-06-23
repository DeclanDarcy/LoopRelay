namespace CommandCenter.Decisions.Models;

public sealed record ExecutionDecisionContext(
    IReadOnlyList<ExecutionConstraint> Constraints,
    IReadOnlyList<ExecutionDirective> Directives,
    IReadOnlyList<ExecutionDecisionPriority> Priorities,
    IReadOnlyList<ExecutionArchitectureRule> ArchitectureRules,
    IReadOnlyList<ExecutionDecisionConflict> Conflicts,
    IReadOnlyList<string> Diagnostics);
