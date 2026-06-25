using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Models;

public sealed class PushAttemptResult
{
    public bool Succeeded { get; init; }

    public bool Retryable { get; init; }

    public string? Error { get; init; }

    public DateTimeOffset? AttemptedAt { get; init; }

    public ExecutionSessionSummary? Session { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static PushAttemptResult Success(ExecutionSessionSummary session) => new()
    {
        Succeeded = true,
        Retryable = false,
        AttemptedAt = session.PushAttemptedAt,
        Session = session,
        Diagnostics = []
    };

    public static PushAttemptResult Failure(string error, ExecutionSessionSummary? session) => new()
    {
        Succeeded = false,
        Retryable = session?.RepositoryState == RepositoryExecutionState.AwaitingPush,
        Error = error,
        AttemptedAt = session?.PushAttemptedAt,
        Session = session,
        Diagnostics = BuildFailureDiagnostics(error, session)
    };

    private static IReadOnlyList<string> BuildFailureDiagnostics(
        string error,
        ExecutionSessionSummary? session)
    {
        List<string> diagnostics = [error];
        if (session is null)
        {
            diagnostics.Add("Updated execution session state was not available after push failure.");
            return diagnostics;
        }

        if (session.RepositoryState == RepositoryExecutionState.AwaitingPush)
        {
            diagnostics.Add("Execution remains awaiting push and can be retried after the remote issue is resolved.");
        }
        else
        {
            diagnostics.Add($"Execution state after push failure is {session.RepositoryState}.");
        }

        if (session.PushAttemptedAt is not null)
        {
            diagnostics.Add("Push attempt timestamp was persisted.");
        }

        return diagnostics;
    }
}
