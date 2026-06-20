namespace CommandCenter.Backend.Execution;

public interface IExecutionProvider
{
    string Name { get; }

    Task<ExecutionProviderStartResult> StartAsync(ExecutionPrompt prompt, ExecutionSession session);
}
