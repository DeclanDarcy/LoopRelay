namespace LoopRelay.Permissions.Models;

public readonly record struct EvalResult(RuleDecision Decision, string Reason);
