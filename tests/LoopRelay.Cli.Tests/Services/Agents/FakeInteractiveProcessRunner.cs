using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;

namespace LoopRelay.Cli.Tests.Services.Agents;


/// <summary>
/// Serves scripted interactive processes (the codex app-server session) via StartInteractiveAsync and records
/// every start, so <see cref="InteractiveCalls"/> is the real count of processes a caller caused to spawn.
/// The default constructor reuses one process for every start; <see cref="Sequence"/> hands out a different
/// process per start so a test can vary what each individual spawn answers.
/// </summary>
internal sealed class FakeInteractiveProcessRunner : IProcessRunner
{
    private readonly Func<int, FakeAgentProcess> _processForStart;

    public FakeInteractiveProcessRunner(FakeAgentProcess process) => _processForStart = _ => process;

    private FakeInteractiveProcessRunner(Func<int, FakeAgentProcess> processForStart)
        => _processForStart = processForStart;

    /// <summary>One scripted process per start, in order. An unscripted extra start fails the test loudly
    /// rather than silently reusing the last process.</summary>
    public static FakeInteractiveProcessRunner Sequence(params FakeAgentProcess[] processes) =>
        new(index => index < processes.Length
            ? processes[index]
            : throw new InvalidOperationException(
                $"Process start #{index + 1} was not scripted; only {processes.Length} were."));

    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> InteractiveCalls { get; } = new();

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        InteractiveCalls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult<IAgentProcess>(_processForStart(InteractiveCalls.Count - 1));
    }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory) =>
        throw new NotSupportedException();
}
