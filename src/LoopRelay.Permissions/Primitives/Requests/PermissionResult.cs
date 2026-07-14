namespace LoopRelay.Permissions.Primitives.Requests;

public readonly record struct PermissionResult(RuleDecision Decision, string Reason);
