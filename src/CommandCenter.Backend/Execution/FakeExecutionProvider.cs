namespace CommandCenter.Backend.Execution;

public sealed class FakeExecutionProvider : IExecutionProvider
{
    public string Name => "fake";

    public bool FailOnStart { get; set; }

    public ExecutionPrompt? LastPrompt { get; private set; }

    public Task<ExecutionProviderStartResult> StartAsync(ExecutionPrompt prompt, ExecutionSession session)
    {
        LastPrompt = prompt;

        if (FailOnStart)
        {
            throw new InvalidOperationException("Fake execution provider failed to start.");
        }

        return Task.FromResult(new ExecutionProviderStartResult
        {
            ProviderName = Name,
            StartedAt = DateTimeOffset.UtcNow
        });
    }
}
