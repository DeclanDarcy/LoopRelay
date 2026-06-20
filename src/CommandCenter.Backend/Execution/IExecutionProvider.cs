namespace CommandCenter.Backend.Execution;

public interface IExecutionProvider
{
    string Name { get; }

    Task StartAsync(ExecutionContext context, ExecutionSession session);
}
