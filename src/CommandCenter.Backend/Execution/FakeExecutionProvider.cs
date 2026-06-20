namespace CommandCenter.Backend.Execution;

public sealed class FakeExecutionProvider : IExecutionProvider
{
    public string Name => "fake";

    public bool FailOnStart { get; set; }

    public Task StartAsync(ExecutionContext context, ExecutionSession session)
    {
        if (FailOnStart)
        {
            throw new InvalidOperationException("Fake execution provider failed to start.");
        }

        return Task.CompletedTask;
    }
}
