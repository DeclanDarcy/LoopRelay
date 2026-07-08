using LoopRelay.Cli.Abstractions;
using LoopRelay.Completion.Abstractions;

namespace LoopRelay.Cli.Services.Console;

internal sealed class ConsoleCompletionObserver(ILoopConsole _console) : ICompletionObserver
{
    public void Phase(string phase) => _console.Phase(phase);

    public void Info(string text) => _console.Info(text);

    public void Warn(string text) => _console.Warn(text);
}
