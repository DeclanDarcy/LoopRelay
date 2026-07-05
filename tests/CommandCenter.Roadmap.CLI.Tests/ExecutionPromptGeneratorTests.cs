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

        string content = await new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts)).GenerateAsync();

        Assert.Contains("Start with `.agents/specs/a.md`", content, StringComparison.Ordinal);
        Assert.Contains("## Required Execution Disposition", content, StringComparison.Ordinal);
        Assert.Contains("| Status | Epic Complete OR Continue Required OR Execution Blocked |", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_specs_enters_blocked_error()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.OperationalContext, "ops");
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "epic");

        await Assert.ThrowsAsync<RoadmapStepException>(() => new ExecutionPromptGenerator(repo.Artifacts, new ArtifactLifecycleStore(repo.Artifacts)).GenerateAsync());
    }
}
