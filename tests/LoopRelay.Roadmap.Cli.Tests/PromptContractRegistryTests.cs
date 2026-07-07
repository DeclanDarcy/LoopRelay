using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class PromptContractRegistryTests
{
    [Fact]
    public void Registry_has_one_contract_for_every_projection()
    {
        var projections = new Cli.ProjectionRegistry();
        var contracts = new Cli.PromptContractRegistry(projections);

        foreach (Cli.ProjectionDefinition projection in projections.All)
        {
            Assert.Equal(projection.RuntimePromptName, contracts.Get(projection.RuntimePromptName).RequiredProjectionRuntimePrompt);
        }
    }

    [Fact]
    public async Task EmitSnapshot_writes_contract_markdown()
    {
        using var repo = new TempRepo();
        var registry = new Cli.PromptContractRegistry(new Cli.ProjectionRegistry());

        await registry.EmitSnapshotAsync(repo.Artifacts);

        Assert.Contains("SelectNextEpic", repo.Read(Cli.RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
        Assert.Contains("Optional Inputs", repo.Read(Cli.RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
        Assert.Contains(".agents/archive/epics/*.md", repo.Read(Cli.RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
        Assert.Contains(Cli.RoadmapArtifactPaths.RoadmapDirectoryPattern, repo.Read(Cli.RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_completion_context_declares_completed_epic_archive_glob()
    {
        var registry = new Cli.PromptContractRegistry(new Cli.ProjectionRegistry());

        Cli.PromptContract contract = registry.Get("CreateRoadmapCompletionContext");

        Assert.Empty(contract.RequiredInputs);
        Assert.Contains(Cli.RoadmapArtifactPaths.CompletedEpicsPattern, contract.OptionalInputs);
    }

    [Fact]
    public void Epic_authoring_contracts_declare_promotion_boundary_and_blocking_outputs()
    {
        var registry = new Cli.PromptContractRegistry(new Cli.ProjectionRegistry());

        foreach (string prompt in new[] { "CreateNewEpic", "RealignEpic", "ReimagineEpic" })
        {
            Cli.PromptContract contract = registry.Get(prompt);

            Assert.Equal("ArtifactPromotionService", contract.ArtifactWriter);
            Assert.Equal("EpicAuthoringOutputClassifier+EpicArtifactValidator", contract.ParserName);
            Assert.NotEmpty(contract.BlockingOutputs);
        }
    }

    [Fact]
    public void SplitEpic_contract_declares_interpretation_and_promotion_boundaries()
    {
        var registry = new Cli.PromptContractRegistry(new Cli.ProjectionRegistry());

        Cli.PromptContract contract = registry.Get("SplitEpic");

        Assert.Contains("SplitEpicBundleInterpreter", contract.ArtifactWriter, StringComparison.Ordinal);
        Assert.Contains("ArtifactPromotionService", contract.ArtifactWriter, StringComparison.Ordinal);
        Assert.Contains("SplitEpicBundleInterpreter", contract.ParserName, StringComparison.Ordinal);
        Assert.Contains(Cli.RoadmapArtifactPaths.ActiveEpic, contract.RequiredOutputs);
        Assert.NotEmpty(contract.BlockingOutputs);
    }
}
