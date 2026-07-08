using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

internal static class ExecutionPreparationTestSupport
{
    public static ExecutionPreparationProvenanceService CreateProvenance(TempRepo repo) =>
        new(repo.Artifacts, new ExecutionPreparationManifestStore(repo.Artifacts));

    public static async Task<ExecutionPreparationProvenanceService> SeedMilestoneSpecsAsync(
        TempRepo repo,
        params string[] specPaths)
    {
        ExecutionPreparationProvenanceService provenance = CreateProvenance(repo);
        await provenance.RecordMilestoneSpecsAsync(specPaths);
        return provenance;
    }

    public static async Task SeedOperationalContextAsync(
        ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(RoadmapArtifactPaths.OperationalContext, content);
        await provenance.RecordOperationalContextAsync(content);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.OperationalContext, ArtifactLifecycleState.Ready);
    }

    public static async Task SeedExecutionPromptAsync(
        ExecutionPreparationProvenanceService provenance,
        TempRepo repo,
        string content)
    {
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, content);
        await provenance.RecordExecutionPromptAsync(content);
        await new ArtifactLifecycleStore(repo.Artifacts).UpsertAsync(RoadmapArtifactPaths.ExecutionPrompt, ArtifactLifecycleState.Ready);
    }
}
