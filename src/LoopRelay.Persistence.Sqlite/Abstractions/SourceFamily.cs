namespace LoopRelay.Persistence.Sqlite.Abstractions;

/// <summary>
/// A source-of-truth evidence family whose content participates in a derived snapshot's
/// invalidation fingerprint. Per-family fingerprinting keeps invalidation scoped: a change to
/// one family only busts the derived kinds that depend on it (the doc's "single global repo
/// version would over-invalidate and defeat the cache").
/// </summary>
public enum SourceFamily
{
    Decisions,
    Reasoning,
    OperationalContext,
    Execution,
    Handoff,
    Git,
    DecisionSession
}
