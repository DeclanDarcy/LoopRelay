namespace LoopRelay.Permissions.Primitives;

public readonly record struct CacheEntry(RuleDecision Decision, string Reason);
