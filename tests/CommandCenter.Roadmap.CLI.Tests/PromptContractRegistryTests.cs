using CommandCenter.Roadmap.Cli;

namespace CommandCenter.Roadmap.CLI.Tests;

public sealed class PromptContractRegistryTests
{
    [Fact]
    public void Registry_has_one_contract_for_every_projection()
    {
        var projections = new ProjectionRegistry();
        var contracts = new PromptContractRegistry(projections);

        foreach (ProjectionDefinition projection in projections.All)
        {
            Assert.Equal(projection.RuntimePromptName, contracts.Get(projection.RuntimePromptName).RequiredProjectionRuntimePrompt);
        }
    }

    [Fact]
    public async Task EmitSnapshot_writes_contract_markdown()
    {
        using var repo = new TempRepo();
        var registry = new PromptContractRegistry(new ProjectionRegistry());

        await registry.EmitSnapshotAsync(repo.Artifacts);

        Assert.Contains("SelectNextEpic", repo.Read(RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
    }
}
