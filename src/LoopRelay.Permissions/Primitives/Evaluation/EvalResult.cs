namespace LoopRelay.Permissions.Primitives;

public readonly record struct EvalResult(RuleDecision Decision, string Reason);
