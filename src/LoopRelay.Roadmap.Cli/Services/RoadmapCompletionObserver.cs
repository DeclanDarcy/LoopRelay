using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Roadmap.Cli;

internal sealed class RoadmapCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
