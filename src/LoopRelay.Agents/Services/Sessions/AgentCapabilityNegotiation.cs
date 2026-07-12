using LoopRelay.Agents.Models.Sessions;

namespace LoopRelay.Agents.Services.Sessions;

/// <summary>
/// The gateway's pre-launch capability negotiation (M7, D5): a session spec requiring a
/// capability the runtime does not declare yields a typed <see cref="AgentCapabilityException"/>
/// — never a silent fallback to another provider or a degraded mode. With Codex (which declares
/// every capability here) no production spec can fail negotiation today; the contract exists so
/// a future provider is additive and its gaps are typed from the first send.
/// </summary>
public static class AgentCapabilityNegotiation
{
    public static void EnsureCanOpenSession(AgentRuntimeCapabilities capabilities, AgentSessionSpec spec)
    {
        if (!capabilities.PersistentSessions)
        {
            throw new AgentCapabilityException(
                nameof(AgentRuntimeCapabilities.PersistentSessions),
                $"provider `{capabilities.Provider}` does not support persistent sessions.");
        }

        if (spec.ResumeThreadId is not null && !capabilities.SessionResume)
        {
            throw new AgentCapabilityException(
                nameof(AgentRuntimeCapabilities.SessionResume),
                $"provider `{capabilities.Provider}` does not support session resume.");
        }
    }

    public static void EnsureCanRunOneShot(AgentRuntimeCapabilities capabilities)
    {
        if (!capabilities.OneShotExecution)
        {
            throw new AgentCapabilityException(
                nameof(AgentRuntimeCapabilities.OneShotExecution),
                $"provider `{capabilities.Provider}` does not support one-shot execution.");
        }
    }
}
