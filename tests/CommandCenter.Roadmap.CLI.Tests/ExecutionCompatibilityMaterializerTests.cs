using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

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

        await new ExecutionCompatibilityMaterializer(repo.Artifacts).MaterializeAsync();

        Assert.Contains(".agents/milestones/m001.md", repo.Read(".agents/plan.md"), StringComparison.Ordinal);
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

        await Assert.ThrowsAsync<RoadmapStepException>(() => new ExecutionCompatibilityMaterializer(repo.Artifacts).MaterializeAsync());
    }
}
