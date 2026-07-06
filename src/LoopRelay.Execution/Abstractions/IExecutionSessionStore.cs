using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IExecutionSessionStore
{
    Task<IReadOnlyList<ExecutionSession>> LoadAsync();

    Task SaveAsync(IReadOnlyList<ExecutionSession> sessions);
}
