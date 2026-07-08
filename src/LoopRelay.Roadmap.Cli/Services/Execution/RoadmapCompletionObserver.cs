using LoopRelay.Completion.Abstractions;
using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services.Execution;

internal sealed class RoadmapCompletionObserver(ILoopConsole _console) : ICompletionObserver
{
    public void Phase(string phase) => _console.Phase(phase);

    public void Info(string text) => _console.Info(text);

    public void Warn(string text) => _console.Warn(text);
}
