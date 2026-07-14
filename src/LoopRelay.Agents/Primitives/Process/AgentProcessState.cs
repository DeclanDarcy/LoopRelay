namespace LoopRelay.Agents.Primitives.Process;

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
