using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Primitives.Evaluation;

public readonly record struct EvalResult(RuleDecision Decision, string Reason);
