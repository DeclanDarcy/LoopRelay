using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapPromptContextBuilderTests
{
    [Fact]
    public async Task Selection_context_contains_projection_completion_roadmap_and_retired_epics()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "current strategic state");
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapFile, "roadmap root");
        repo.Write(".agents/roadmap/b.md", "roadmap b");

        string context = await new Cli.RoadmapPromptContextBuilder(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo)).BuildSelectionContextAsync(
            "projection",
            [new Cli.RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]);

        Assert.Contains("projection", context, StringComparison.Ordinal);
        Assert.Contains("current strategic state", context, StringComparison.Ordinal);
        Assert.Contains("roadmap root", context, StringComparison.Ordinal);
        Assert.Contains("## Retired Epics", context, StringComparison.Ordinal);
        Assert.Contains("EPIC-001", context, StringComparison.Ordinal);
        Assert.Contains("Retired Epic", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_context_rejects_raw_project_context_markers()
    {
        using var repo = new TempRepo();
        var builder = new Cli.RoadmapPromptContextBuilder(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo));

        Assert.Throws<Cli.RoadmapStepException>(() => builder.BuildAuditContext("<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->", "epic"));
    }
}
