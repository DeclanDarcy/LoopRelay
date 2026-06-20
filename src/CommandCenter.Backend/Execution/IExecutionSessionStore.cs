namespace CommandCenter.Backend.Execution;

public interface IExecutionSessionStore
{
    Task<IReadOnlyList<ExecutionSession>> LoadAsync();

    Task SaveAsync(IReadOnlyList<ExecutionSession> sessions);
}
