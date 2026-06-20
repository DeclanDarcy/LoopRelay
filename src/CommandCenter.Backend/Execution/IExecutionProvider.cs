namespace CommandCenter.Backend.Execution;

public interface IExecutionProvider
{
    string Name { get; }

    Task StartAsync(ExecutionPrompt prompt, ExecutionSession session);
}
