using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class RoadmapPromptContextBuilderTests
{
    [Fact]
    public async Task Selection_context_contains_projection_completion_roadmap_and_retired_epics()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "current strategic state");
        repo.Write(RoadmapArtifactPaths.RoadmapFile, "roadmap root");
        repo.Write(".agents/roadmap/b.md", "roadmap b");

        string context = await new RoadmapPromptContextBuilder(repo.Artifacts).BuildSelectionContextAsync(
            "projection",
            [new RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]);

        Assert.Contains("projection", context, StringComparison.Ordinal);
        Assert.Contains("current strategic state", context, StringComparison.Ordinal);
        Assert.Contains("roadmap root", context, StringComparison.Ordinal);
        Assert.Contains("## Retired Epics", context, StringComparison.Ordinal);
        Assert.Contains("EPIC-001", context, StringComparison.Ordinal);
        Assert.Contains("Retired Epic", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_context_rejects_raw_north_star_markers()
    {
        var builder = new RoadmapPromptContextBuilder(new TempRepo().Artifacts);

        Assert.Throws<RoadmapStepException>(() => builder.BuildAuditContext("<!-- BEGIN NORTH-STAR FILE: 01-purpose.md -->", "epic"));
    }
}
