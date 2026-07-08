namespace LoopRelay.Agents.Models.Sessions;

/// <summary>
/// A codex session resume attempt failed (rollout deleted, unknown thread id, protocol drift, or the
/// process died during the eager handshake). RECOVERABLE by contract: the caller falls back to opening a
/// fresh session — which is why this is typed rather than a bare InvalidOperationException.
/// </summary>
public sealed class AgentSessionResumeException : Exception
{
    public AgentSessionResumeException(string message)
        : base(message)
    {
    }

    public AgentSessionResumeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
