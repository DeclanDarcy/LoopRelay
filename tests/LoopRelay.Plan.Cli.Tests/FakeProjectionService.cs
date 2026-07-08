using System.Collections.Concurrent;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Projections;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;

namespace LoopRelay.Plan.Cli.Tests;

internal sealed class FakeProjectionService(string content = "PROJECT CONTEXT PROJECTION") : IProjectContextProjectionService
{
    public int EnsureFreshCalls { get; private set; }
    public int EvaluateFreshnessCalls { get; private set; }
    public List<string> RuntimePromptNames { get; } = new();

    public Task<ProjectContextProjectionResult> EnsureFreshAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        EnsureFreshCalls++;
        RuntimePromptNames.Add(runtimePromptName);
        return Task.FromResult(new ProjectContextProjectionResult(
            new ProjectionDefinition(
                runtimePromptName,
                $"ProjectionFor{runtimePromptName}",
                ProjectionArtifactPaths.ProjectionPaths[runtimePromptName],
                "# Test Projection",
                runtimePromptName),
            content,
            Generated: true,
            ProjectionStaleStatus.Fresh,
            []));
    }

    public Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        EvaluateFreshnessCalls++;
        RuntimePromptNames.Add(runtimePromptName);
        return Task.FromResult(ProjectionFreshness.Fresh);
    }
}
