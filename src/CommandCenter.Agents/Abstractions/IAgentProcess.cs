using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Abstractions;

public interface IAgentProcess : IAsyncDisposable
{
    int ProcessId { get; }

    AgentProcessState State { get; }

    int? ExitCode { get; }

    bool HasExited { get; }

    Task Completion { get; }

    Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default);

    Task WritePromptAsync(string text, CancellationToken cancellationToken = default);

    Task CompleteInputAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> ReadOutputLinesAsync(CancellationToken cancellationToken = default);
}
