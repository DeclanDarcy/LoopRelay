namespace LoopRelay.Permissions.Primitives;

public readonly record struct PermissionResult(RuleDecision Decision, string Reason);
