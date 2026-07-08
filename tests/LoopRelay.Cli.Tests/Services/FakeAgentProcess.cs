using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Primitives;

namespace LoopRelay.Cli.Tests.Services;


/// <summary>
/// A scripted interactive codex process: streams <c>lines</c> from ReadOutputLinesAsync (optionally hanging
/// afterwards to exercise the scrape timeout), records prompts written, and tracks disposal.
/// </summary>
internal sealed class FakeAgentProcess(IEnumerable<string> lines, bool hangAfterLines = false) : IAgentProcess
{
    private readonly IReadOnlyList<string> lines = lines.ToList();

    public List<string> PromptsWritten { get; } = new();
    public int LinesEmitted { get; private set; }
    public bool Disposed { get; private set; }

    public int ProcessId => 1;
    public AgentProcessState State => AgentProcessState.Running;
    public int? ExitCode => null;
    public bool HasExited => false;
    public Task Completion => Task.CompletedTask;

    public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) =>
        WritePromptAsync(standardInput, cancellationToken);

    public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        PromptsWritten.Add(text);
        return Task.CompletedTask;
    }

    public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> ReadOutputLinesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (string line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LinesEmitted++;
            yield return line;
        }

        if (hangAfterLines)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
