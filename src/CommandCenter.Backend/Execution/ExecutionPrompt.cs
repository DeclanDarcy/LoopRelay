namespace CommandCenter.Backend.Execution;

public sealed class ExecutionPrompt
{
    public string Text { get; init; } = string.Empty;

    public ExecutionPromptMetadata Metadata { get; init; } = new();
}
