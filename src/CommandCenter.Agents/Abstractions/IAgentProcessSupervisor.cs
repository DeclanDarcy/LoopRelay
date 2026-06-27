using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentProcessSupervisor : IAsyncDisposable
{
    AgentProcessState State { get; }

    int? ExitCode { get; }

    Task<AgentProcessSupervisionResult> Completion { get; }

    IReadOnlyList<AgentProcessEvent> Events { get; }

    Task<AgentProcessSupervisionResult> ObserveCompletionAsync(
        Func<int?, Task>? onExit = null,
        CancellationToken cancellationToken = default);

    Task CancelAsync(CancellationToken cancellationToken = default);
}
