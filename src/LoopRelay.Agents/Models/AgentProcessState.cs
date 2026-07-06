namespace LoopRelay.Agents.Models;

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
