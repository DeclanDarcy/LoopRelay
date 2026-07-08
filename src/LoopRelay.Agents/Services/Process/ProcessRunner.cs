using System.Diagnostics;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;

namespace LoopRelay.Agents.Services.Process;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var stopwatch = Stopwatch.StartNew();
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)
                                                   ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        stopwatch.Stop();

        return new ProcessRunResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await standardOutputTask,
            StandardError = await standardErrorTask,
            Duration = stopwatch.Elapsed
        };
    }

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        AgentProcess process = StartAgentProcess(startInfo, fileName);
        process.StartCompletionObservation();
        // Drain stderr so a verbose child (e.g. codex `exec` without --json) cannot deadlock by filling
        // the unread stderr pipe buffer — nothing else reads this interactive process's standard error.
        process.StartErrorDrain();

        return Task.FromResult<IAgentProcess>(process);
    }

    private static AgentProcess StartAgentProcess(ProcessStartInfo startInfo, string fileName)
    {
        System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)
                                             ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        return new AgentProcess(process);
    }
}
