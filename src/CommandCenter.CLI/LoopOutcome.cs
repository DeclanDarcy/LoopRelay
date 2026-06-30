namespace CommandCenter.Cli;

internal enum LoopOutcome
{
    EpicCompleted,
    Cancelled,
    Failed,
}

/// <summary>A verify/agent gate failed; aborts the loop (never retried).</summary>
internal sealed class LoopStepException(string message) : Exception(message);
