namespace CommandCenter.Cli;

internal enum LoopOutcome
{
    EpicCompleted,
    Cancelled,
    Failed,
    Stalled,
}

/// <summary>A verify/agent gate failed; aborts the loop (never retried).</summary>
internal sealed class LoopStepException : Exception
{
    public LoopStepException(string message) : base(message)
    {
    }

    // Preserves the underlying failure (stack trace + type) as InnerException when a gate aborts because a
    // lower-level operation threw — e.g. a store exception while archiving the operational delta.
    public LoopStepException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
