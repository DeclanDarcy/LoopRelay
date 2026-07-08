namespace LoopRelay.Agents.Primitives;

public enum AgentProcessState
{
    Created,
    Starting,
    Running,
    Stopping,
    Exited,
    Failed,
    Canceled,
    Disposed
}
