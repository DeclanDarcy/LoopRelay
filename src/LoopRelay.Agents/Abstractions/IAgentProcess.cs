using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;

namespace LoopRelay.Agents.Abstractions;

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

    /// <summary>
    /// The retained tail of the process's standard-error stream, or null when nothing was captured.
    /// Default implementation returns null so fakes that never touch stderr keep compiling untouched.
    /// </summary>
    string? ErrorSnapshot => null;
}
