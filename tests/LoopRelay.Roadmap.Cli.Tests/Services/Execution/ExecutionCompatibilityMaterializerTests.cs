using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

public sealed class ExecutionCompatibilityMaterializerTests
{
    [Fact]
    public async Task Materializes_plan_and_milestones_from_specs()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "prompt");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "epic");
        repo.Write(".agents/specs/a.md", """
        # Spec

        ## Acceptance Criteria

        - Do the thing
        """);
        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "ops");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "prompt");

        await new Cli.Services.ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync();

        Assert.Contains(".agents/milestones/m001.md", repo.Read(RoadmapArtifactPaths.ExecutionPlan), StringComparison.Ordinal);
        Assert.Contains("Do the thing", repo.Read(".agents/milestones/m001.md"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Materializer_blocks_when_no_auditable_checklist_exists()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(RoadmapArtifactPaths.ExecutionPrompt, "prompt");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "epic");
        repo.Write(".agents/specs/a.md", "no checklist");
        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "ops");
        await ExecutionPreparationTestSupport.SeedExecutionPromptAsync(provenance, repo, "prompt");

        await Assert.ThrowsAsync<RoadmapStepException>(() => new Cli.Services.ExecutionCompatibilityMaterializer(repo.Artifacts, provenance).MaterializeAsync());
    }
}
