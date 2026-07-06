namespace LoopRelay.Execution.Primitives;

public sealed class ExecutionProviderException : InvalidOperationException
{
    public ExecutionProviderException(string code, string message)
        : base($"{code}: {message}")
    {
        Code = code;
    }

    public string Code { get; }
}
