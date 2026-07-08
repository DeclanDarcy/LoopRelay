using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal sealed class FakeCompletedEpicArchiveService : ICompletedEpicArchiveService
{
    public List<CompletedEpicArchiveRequest> Requests { get; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public CompletedEpicArchiveResult Result { get; set; } = new(
        1,
        ".agents/archive/epics/1",
        ".agents/archive/epics/1.md",
        "# Completed Epic");

    public Task<CompletedEpicArchiveResult> ArchiveAndSynthesizeAsync(
        CompletedEpicArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (ExceptionToThrow is not null)
        {
            return Task.FromException<CompletedEpicArchiveResult>(ExceptionToThrow);
        }

        return Task.FromResult(Result);
    }
}
