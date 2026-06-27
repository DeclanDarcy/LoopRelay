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

        Process process = Process.Start(startInfo)
                          ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        if (onStandardOutput is not null)
        {
            _ = Task.Run(() => ReadLinesAsync(process.StandardOutput, onStandardOutput));
        }

        if (onStandardError is not null)
        {
            _ = Task.Run(() => ReadLinesAsync(process.StandardError, onStandardError));
        }

        if (onExit is not null)
        {
            _ = Task.Run(async () =>
            {
                await process.WaitForExitAsync();
                await onExit(process.ExitCode);
                process.Dispose();
            });
        }

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();
        }

        await Task.Delay(ImmediateExitProbeDelay);

        return new ProcessStartResult
        {
            ProcessId = process.Id,
            HasExited = process.HasExited,
            ExitCode = process.HasExited ? process.ExitCode : null
        };
    }

    private static async Task ReadLinesAsync(StreamReader reader, Func<string, Task> onLine)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            await onLine(line);
        }
    }
}
