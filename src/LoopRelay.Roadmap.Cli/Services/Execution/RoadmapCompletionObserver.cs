using LoopRelay.Completion.Abstractions;
using LoopRelay.Roadmap.Cli.Abstractions;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class RoadmapCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
