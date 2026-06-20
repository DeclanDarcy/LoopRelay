namespace CommandCenter.Backend.Execution;

public sealed class NoopExecutionProvider : IExecutionProvider
{
    public string Name => "none";

    public Task StartAsync(ExecutionPrompt prompt, ExecutionSession session)
    {
        throw new InvalidOperationException("No execution provider is configured.");
    }
}
