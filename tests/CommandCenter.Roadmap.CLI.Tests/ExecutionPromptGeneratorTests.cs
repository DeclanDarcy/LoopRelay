using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class ExecutionPromptGeneratorTests
{
    [Fact]
    public async Task Execution_prompt_identifies_first_executable_spec()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "epic");
        repo.Write(".agents/specs/a.md", "spec a");
        repo.Write(".agents/specs/b.md", "spec b");
        ExecutionPreparationProvenanceService provenance = await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(
            repo,
            ".agents/specs/a.md",
            ".agents/specs/b.md");
        await ExecutionPreparationTestSupport.SeedOperationalContextAsync(provenance, repo, "ops");

        string content = await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync();

        Assert.Contains("Start with `.agents/specs/a.md`", content, StringComparison.Ordinal);
        Assert.Contains("## Required Execution Disposition", content, StringComparison.Ordinal);
        Assert.Contains($"| Status | {ExecutionDispositionProtocol.StatusOptionsText} |", content, StringComparison.Ordinal);
        Assert.Contains($"| Next Step | {ExecutionDispositionProtocol.CommandOptionsText} |", content, StringComparison.Ordinal);
        foreach (string pair in ExecutionDispositionProtocol.ValidPairDescriptions)
        {
            Assert.Contains($"- `{pair}`", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Missing_specs_enters_blocked_error()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "epic");

        ExecutionPreparationProvenanceService provenance = ExecutionPreparationTestSupport.CreateProvenance(repo);
        await Assert.ThrowsAsync<RoadmapStepException>(() => new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts), provenance).GenerateAsync());
    }
}
