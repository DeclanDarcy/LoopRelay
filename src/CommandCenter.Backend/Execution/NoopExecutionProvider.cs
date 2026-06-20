namespace CommandCenter.Backend.Execution;

public sealed class NoopExecutionProvider : IExecutionProvider
{
    public string Name => "none";
}
