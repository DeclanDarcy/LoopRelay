using System.Diagnostics;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

public sealed class ProcessRunner : IProcessRunner
{
    private static readonly TimeSpan ImmediateExitProbeDelay = TimeSpan.FromMilliseconds(250);

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
        using Process process = Process.Start(startInfo)
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

    public async Task<ProcessStartResult> StartAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string? standardInput = null,
        Func<string, Task>? onStandardOutput = null,
        Func<string, Task>? onStandardError = null,
        Func<int?, Task>? onExit = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = onStandardOutput is not null,
            RedirectStandardError = onStandardError is not null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        AgentProcess process = StartAgentProcess(startInfo, fileName);
        var eventStream = new AgentProcessEventStream();
        var supervisor = new AgentProcessSupervisor(process, eventStream);
        process.StartCompletionObservation();

        if (onStandardOutput is not null)
        {
            _ = Task.Run(() => ReadLinesAsync(
                process,
                eventStream,
                process.StandardOutput,
                AgentProcessOutputStream.StandardOutput,
                onStandardOutput));
        }

        if (onStandardError is not null)
        {
            _ = Task.Run(() => ReadLinesAsync(
                process,
                eventStream,
                process.StandardError,
                AgentProcessOutputStream.StandardError,
                onStandardError));
        }

        if (onExit is not null)
        {
            _ = Task.Run(async () =>
            {
                await supervisor.ObserveCompletionAsync(onExit);
            });
        }

        if (standardInput is not null)
        {
            await process.WriteStandardInputAsync(standardInput);
        }

        await Task.Delay(ImmediateExitProbeDelay);

        return new ProcessStartResult
        {
            ProcessId = process.ProcessId,
            HasExited = process.HasExited,
            ExitCode = process.HasExited ? process.ExitCode : null
        };
    }

    private static AgentProcess StartAgentProcess(ProcessStartInfo startInfo, string fileName)
    {
        Process process = Process.Start(startInfo)
                          ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        return new AgentProcess(process);
    }

    private static async Task ReadLinesAsync(
        IAgentProcess process,
        AgentProcessEventStream eventStream,
        StreamReader reader,
        AgentProcessOutputStream outputStream,
        Func<string, Task> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            eventStream.Record(
                process.ProcessId,
                AgentProcessEventKind.ProcessOutput,
                process.State,
                process.ExitCode,
                outputStream,
                line);
            await onLine(line);
        }
    }
}
