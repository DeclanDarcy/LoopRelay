using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Tests.Services;

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
        Assert.Contains("Optional Inputs", repo.Read(RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
        Assert.Contains(".agents/archive/epics/*.md", repo.Read(RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
        Assert.Contains(RoadmapArtifactPaths.RoadmapDirectoryPattern, repo.Read(RoadmapArtifactPaths.PromptContracts), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_completion_context_declares_completed_epic_archive_glob()
    {
        var registry = new PromptContractRegistry(new ProjectionRegistry());

        PromptContract contract = registry.Get("CreateRoadmapCompletionContext");

        Assert.Empty(contract.RequiredInputs);
        Assert.Contains(RoadmapArtifactPaths.CompletedEpicsPattern, contract.OptionalInputs);
    }

    [Fact]
    public void Epic_authoring_contracts_declare_promotion_boundary_and_blocking_outputs()
    {
        var registry = new PromptContractRegistry(new ProjectionRegistry());

        foreach (string prompt in new[] { "CreateNewEpic", "RealignEpic", "ReimagineEpic" })
        {
            PromptContract contract = registry.Get(prompt);

            Assert.Equal("ArtifactPromotionService", contract.ArtifactWriter);
            Assert.Equal("EpicAuthoringOutputClassifier+EpicArtifactValidator", contract.ParserName);
            Assert.NotEmpty(contract.BlockingOutputs);
        }
    }

    [Fact]
    public void SplitEpic_contract_declares_interpretation_and_promotion_boundaries()
    {
        var registry = new PromptContractRegistry(new ProjectionRegistry());

        PromptContract contract = registry.Get("SplitEpic");

        Assert.Contains("SplitEpicBundleInterpreter", contract.ArtifactWriter, StringComparison.Ordinal);
        Assert.Contains("ArtifactPromotionService", contract.ArtifactWriter, StringComparison.Ordinal);
        Assert.Contains("SplitEpicBundleInterpreter", contract.ParserName, StringComparison.Ordinal);
        Assert.Contains(RoadmapArtifactPaths.ActiveEpic, contract.RequiredOutputs);
        Assert.NotEmpty(contract.BlockingOutputs);
    }
}
