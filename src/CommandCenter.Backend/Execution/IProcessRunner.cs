namespace CommandCenter.Backend.Execution;

public interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory);

    Task<ProcessStartResult> StartAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string? standardInput = null,
        Func<string, Task>? onStandardOutput = null,
        Func<string, Task>? onStandardError = null,
        Func<int?, Task>? onExit = null);
}
