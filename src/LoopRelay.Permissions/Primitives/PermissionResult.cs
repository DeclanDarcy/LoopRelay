namespace LoopRelay.Permissions.Models;

public readonly record struct PermissionResult(RuleDecision Decision, string Reason);
