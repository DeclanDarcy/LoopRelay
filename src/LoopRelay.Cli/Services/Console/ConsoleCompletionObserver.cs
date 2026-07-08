using LoopRelay.Cli.Abstractions;
using LoopRelay.Completion.Abstractions;

namespace LoopRelay.Cli.Services;

internal sealed class ConsoleCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
