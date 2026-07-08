using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Primitives;

namespace LoopRelay.Plan.Cli.Tests.Services.Agents;

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
