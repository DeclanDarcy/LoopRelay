using LoopRelay.Roadmap.Cli;
using ExecutionCompatibilityMaterializer = LoopRelay.Roadmap.Cli.ExecutionCompatibilityMaterializer;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class ExecutionCompatibilityMaterializerTests
{
    [Fact]
    public async Task Materializes_plan_and_milestones_from_specs()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "prompt");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, "epic");
        repo.Write(".agents/specs/a.md", """
        # Spec

        ## Acceptance Criteria

        - Do the thing
        """);
        Cli.ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "ops");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "prompt");

        await new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        Assert.Contains(".agents/milestones/m001.md", repo.Read(Cli.RoadmapArtifactPaths.ExecutionPlan), StringComparison.Ordinal);
        Assert.Contains("Do the thing", repo.Read(".agents/milestones/m001.md"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Materializer_blocks_when_no_auditable_checklist_exists()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(Cli.RoadmapArtifactPaths.ExecutionPrompt, "prompt");
        repo.Write(Cli.RoadmapArtifactPaths.ActiveEpic, "epic");
        repo.Write(".agents/specs/a.md", "no checklist");
        Cli.ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "ops");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "prompt");

        await Assert.ThrowsAsync<Cli.RoadmapStepException>(() => new ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync());
    }
}
