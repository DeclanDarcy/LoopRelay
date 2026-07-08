using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal static class ExecutionPreparationTestSupport
{
    public static Cli.ExecutionPreparationProvenanceService CreateProvenance(TempRepo repo) =>
        new(repo.Artifacts, new Cli.ExecutionPreparationManifestStore(repo.Artifacts));

    public static async Task<Cli.ExecutionPreparationProvenanceService> SeedMilestoneSpecsAsync(
        TempRepo repo,
        params string[] specPaths)
    {
        Cli.ExecutionPreparationProvenanceService provenance = CreateProvenance(repo);
        await provenance.RecordMilestoneSpecsAsync(specPaths);
        return provenance;
    }

    public static async Task SeedOperationalContextAsync(
        Cli.ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, content);
        await provenance.RecordOperationalContextAsync(content);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.OperationalContext, Cli.ArtifactLifecycleState.Ready);
    }

    public static async Task SeedExecutionPromptAsync(
        Cli.ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, content);
        await provenance.RecordExecutionPromptAsync(content);
        await new Cli.ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(Cli.RoadmapArtifactPaths.ExecutionPrompt, Cli.ArtifactLifecycleState.Ready);
    }
}
