using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;

namespace LoopRelay.Plan.Cli.Tests.Services;


/// <summary>
/// Scripts <see cref="IProcessRunner.RunAsync"/> (no real git). Records every call in
/// invocation order, and returns whatever <see cref="Handler"/> produces (or a zero-exit success when Handler
/// is null). <see cref="OnRunAsync"/> is an async hook invoked (before Handler) for every call, so a test can
/// simulate filesystem side effects against the in-memory artifact store. StartInteractiveAsync throws — the
/// publisher only ever runs RunAsync.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> Calls { get; } = new();

    // Handler receives the working directory too, so a test can script the .agents submodule
    // (workingDirectory ends in ".agents") differently from the parent repo.
    public Func<string, IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    /// <summary>Async side-effect hook: (fileName, args, workingDirectory) → Task, run before Handler.</summary>
    public Func<string, IReadOnlyList<string>, string, Task>? OnRunAsync { get; set; }

    public async Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Calls.Add((fileName, arguments, workingDirectory));
        if (OnRunAsync is not null)
        {
            await OnRunAsync(fileName, arguments, workingDirectory);
        }

        return Handler?.Invoke(workingDirectory, arguments) ?? Ok();
    }

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static ProcessRunResult Ok(string stdout = "") =>
        new() { ExitCode = 0, StandardOutput = stdout, StandardError = string.Empty, Duration = TimeSpan.Zero };

    public static ProcessRunResult Fail(string stderr, int exitCode = 1, string stdout = "") =>
        new() { ExitCode = exitCode, StandardOutput = stdout, StandardError = stderr, Duration = TimeSpan.Zero };
}
