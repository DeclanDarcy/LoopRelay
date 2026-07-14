using LoopRelay.Completion.Models;
using LoopRelay.Completion.Models.Prompts;

namespace LoopRelay.Completion.Abstractions;

public interface ICompletionPromptRunner
{
    Task<string> RunAsync(
        CompletionRuntimePromptInvocation invocation,
        CancellationToken cancellationToken = default);
}
