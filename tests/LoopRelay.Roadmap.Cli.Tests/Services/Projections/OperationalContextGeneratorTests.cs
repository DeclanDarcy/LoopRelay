using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Projections;

public sealed class OperationalContextGeneratorTests
{
    [Fact]
    public async Task Operational_context_references_active_epic_and_ordered_specs()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "active epic");
        repo.Write(".agents/specs/b.md", "spec b");
        repo.Write(".agents/specs/a.md", "spec a");
        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(
            repo,
            ".agents/specs/b.md",
            ".agents/specs/a.md");

        string content = await new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();

        Assert.Contains("active epic", content, StringComparison.Ordinal);
        Assert.True(content.IndexOf(".agents/specs/a.md", StringComparison.Ordinal) < content.IndexOf(".agents/specs/b.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Operational_context_rejects_raw_project_context_markers()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->");
        repo.Write(".agents/specs/a.md", "spec");
        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/a.md");

        await Assert.ThrowsAsync<RoadmapStepException>(() => new OperationalContextGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync());
    }
}
