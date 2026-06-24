namespace CommandCenter.Decisions.Models;

public sealed record DecisionLifecycleEligibilityProjection(
    Guid RepositoryId,
    IReadOnlyList<DecisionLifecycleEntityEligibility> Candidates,
    IReadOnlyList<DecisionLifecycleEntityEligibility> Proposals,
    IReadOnlyList<DecisionLifecycleEntityEligibility> Decisions,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionLifecycleEntityEligibility(
    string EntityKind,
    string EntityId,
    string CurrentState,
    IReadOnlyList<DecisionLifecycleActionEligibility> AllowedActions,
    IReadOnlyList<DecisionLifecycleActionEligibility> BlockedActions,
    IReadOnlyList<string> AllowedNextStates,
    IReadOnlyList<DecisionLifecycleBlockedState> BlockedNextStates,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionLifecycleActionEligibility(
    string CommandName,
    string DisplayName,
    string TargetState,
    bool IsAllowed,
    IReadOnlyList<string> RequiredInputs,
    string? Reason,
    string GoverningRule);

public sealed record DecisionLifecycleBlockedState(
    string State,
    string Reason,
    string GoverningRule);
