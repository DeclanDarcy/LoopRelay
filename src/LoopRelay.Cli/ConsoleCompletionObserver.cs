using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Extensions;
using LoopRelay.Agents.Services;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Infrastructure.Artifacts;
using LoopRelay.Infrastructure.Diagnostics;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Configuration;
using LoopRelay.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace LoopRelay.Cli;

internal sealed class ConsoleCompletionObserver(ILoopConsole console) : ICompletionObserver
{
    public void Phase(string phase) => console.Phase(phase);

    public void Info(string text) => console.Info(text);

    public void Warn(string text) => console.Warn(text);
}
