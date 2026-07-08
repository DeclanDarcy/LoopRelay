namespace LoopRelay.Permissions.Models;

public readonly record struct CacheEntry(RuleDecision Decision, string Reason);
