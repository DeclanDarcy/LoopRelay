using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IExecutionProvider
{
    string Name { get; }

    bool SupportsReattach { get; }

    Task<ExecutionProviderStartResult> StartAsync(
        ExecutionPrompt prompt,
        ExecutionSession session,
        IExecutionProviderObserver observer);

    Task<bool> TryReattachAsync(
        ExecutionSession session,
        IExecutionProviderObserver observer);
}
