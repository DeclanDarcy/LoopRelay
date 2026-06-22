using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Modules;

public sealed class NoopExecutionProvider : IExecutionProvider
{
    public string Name => "none";

    public bool SupportsReattach => false;

    public Task<ExecutionProviderStartResult> StartAsync(
        ExecutionPrompt prompt,
        ExecutionSession session,
        IExecutionProviderObserver observer)
    {
        throw new InvalidOperationException("No execution provider is configured.");
    }

    public Task<bool> TryReattachAsync(
        ExecutionSession session,
        IExecutionProviderObserver observer)
    {
        return Task.FromResult(false);
    }
}
