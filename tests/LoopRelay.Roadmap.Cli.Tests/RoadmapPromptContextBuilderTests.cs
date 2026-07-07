using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapPromptContextBuilderTests
{
    [Fact]
    public async Task Selection_context_contains_projection_completion_roadmap_references_and_retired_epics()
    {
        using var repo = new TempRepo();
        repo.Write(Cli.RoadmapArtifactPaths.RoadmapCompletionContext, "current strategic state");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap 001 body must not be injected");
        repo.Write(".agents/roadmap/b.md", "roadmap b body must not be injected");

        string context = await new Cli.RoadmapPromptContextBuilder(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo)).BuildSelectionContextAsync(
            "projection",
            [new Cli.RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]);

        Assert.Contains("projection", context, StringComparison.Ordinal);
        Assert.Contains("current strategic state", context, StringComparison.Ordinal);
        Assert.Contains("## Roadmap Source References", context, StringComparison.Ordinal);
        Assert.Contains(Cli.RoadmapArtifactPaths.RoadmapDirectoryPattern, context, StringComparison.Ordinal);
        Assert.Contains(".agents/roadmap/001-roadmap.md", context, StringComparison.Ordinal);
        Assert.Contains(".agents/roadmap/b.md", context, StringComparison.Ordinal);
        Assert.DoesNotContain("roadmap 001 body must not be injected", context, StringComparison.Ordinal);
        Assert.DoesNotContain("roadmap b body must not be injected", context, StringComparison.Ordinal);
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
