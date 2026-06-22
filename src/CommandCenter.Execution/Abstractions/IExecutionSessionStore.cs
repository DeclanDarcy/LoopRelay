using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Abstractions;

public interface IExecutionSessionStore
{
    Task<IReadOnlyList<ExecutionSession>> LoadAsync();

    Task SaveAsync(IReadOnlyList<ExecutionSession> sessions);
}
