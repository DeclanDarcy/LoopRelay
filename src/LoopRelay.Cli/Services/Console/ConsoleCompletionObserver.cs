using LoopRelay.Cli.Abstractions;
using LoopRelay.Completion.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    private readonly ILoopConsole _console = console;
    public void Phase(string phase) => _console.Phase(phase);

    public void Info(string text) => _console.Info(text);

    public void Warn(string text) => _console.Warn(text);
}
