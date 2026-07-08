using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Primitives.Evaluation;

public readonly record struct CacheEntry(RuleDecision Decision, string Reason);
