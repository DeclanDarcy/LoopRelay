using LoopRelay.Completion.Models;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletionPromptRunner
{
    Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default);
}
