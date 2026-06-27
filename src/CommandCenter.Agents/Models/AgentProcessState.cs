namespace CommandCenter.Agents.Models;

public enum AgentProcessState
{
    Created,
    Starting,
    Running,
    Exited,
    Failed,
    Canceled
}
