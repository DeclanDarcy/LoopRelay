using CommandCenter.Agents.Abstractions;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Execution.Modules;

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
        CodexExecutable executable = executableResolver.Resolve();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        ProcessStartResult result;

        try
        {
            // Text-mode `codex exec` (no --json): this path consumes stdout/stderr as opaque text and
            // detects completion via process exit + handoff-file validation, so Codex's structured JSONL
            // stream is neither parsed nor wanted here. Sandbox and approval are pinned explicitly rather
            // than inherited from the user's ~/.codex/config.toml — danger-full-access is the deliberate
            // (temporary) policy until workspace sandboxing is wired up.
            result = await processRunner.StartAsync(
                executable.Path,
                ["exec", "--cd", session.RepositoryPath, "--sandbox", "danger-full-access", "-c", "approval_policy=\"never\"", "-"],
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
            string exitCode = result.ExitCode?.ToString() ?? "unknown";
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
