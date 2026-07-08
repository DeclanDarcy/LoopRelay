using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Cli.Tests.Services;


/// <summary>
/// Scripts <see cref="IProcessRunner.RunAsync"/> for the CommitGate (no real git). Records every call,
/// and returns whatever <see cref="Handler"/> produces (or a zero-exit success when Handler is null).
/// StartInteractiveAsync throws — the gate only ever runs RunAsync.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> Calls { get; } = new();

    // Handler receives the working directory too, so a test can script the .agents submodule
    // (workingDirectory ends in ".agents") differently from the parent repo.
    public Func<string, IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Calls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult(Handler?.Invoke(workingDirectory, arguments) ?? Ok());
    }

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static ProcessRunResult Ok(string stdout = "") =>
        new() { ExitCode = 0, StandardOutput = stdout, StandardError = string.Empty, Duration = TimeSpan.Zero };

    public static ProcessRunResult Fail(string stderr) =>
        new() { ExitCode = 1, StandardOutput = string.Empty, StandardError = stderr, Duration = TimeSpan.Zero };
}
