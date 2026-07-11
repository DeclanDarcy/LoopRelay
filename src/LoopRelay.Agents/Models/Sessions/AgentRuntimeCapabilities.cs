namespace LoopRelay.Agents.Models.Sessions;

/// <summary>
/// The provider-neutral capability declaration of an agent runtime (M7 Runtime Authority).
/// <see cref="Provider"/> is the runtime's provider identity — evidence for session facts,
/// never a domain classifier a workflow may branch on. The capability booleans are what the
/// gateway negotiates against before launch: a session spec requiring a capability the runtime
/// does not declare yields a typed <see cref="AgentCapabilityException"/> instead of an
/// unrecorded fallback. Session forking has no capability bit because the spec cannot express
/// a fork request yet; the bit is added with the request shape, not before it.
/// </summary>
public sealed record AgentRuntimeCapabilities(
    string Provider,
    bool OneShotExecution,
    bool PersistentSessions,
    bool SessionResume);
