namespace CommandCenter.Backend.Execution;

public sealed class CodexExecutionProvider(
    ICodexExecutableResolver executableResolver,
    IProcessRunner processRunner) : IExecutionProvider
{
    public string Name => "codex";

    public bool SupportsReattach => false;

    public async Task<ExecutionProviderStartResult> StartAsync(
        ExecutionPrompt prompt,
        ExecutionSession session,
        IExecutionProviderObserver observer)
    {
        var executable = executableResolver.Resolve();
        var startedAt = DateTimeOffset.UtcNow;
        ProcessStartResult result;

        try
        {
            result = await processRunner.StartAsync(
                executable.Path,
                ["exec", "--cd", session.RepositoryPath, "-"],
                session.RepositoryPath,
                prompt.Text,
                observer.OnStdOutAsync,
                observer.OnStdErrAsync,
                observer.OnProviderExitedAsync);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            throw new ExecutionProviderException(
                "ProviderLaunchFailed",
                $"Codex process failed to start: {exception.Message}");
        }

        if (result.HasExited)
        {
            var exitCode = result.ExitCode?.ToString() ?? "unknown";
            throw new ExecutionProviderException(
                "ProviderImmediateExit",
                $"Codex process exited immediately with exit code {exitCode}.");
        }

        return new ExecutionProviderStartResult
        {
            ProviderName = Name,
            ExecutablePath = executable.Path,
            ProcessId = result.ProcessId,
            StartedAt = startedAt
        };
    }

    public Task<bool> TryReattachAsync(
        ExecutionSession session,
        IExecutionProviderObserver observer)
    {
        return Task.FromResult(false);
    }
}
