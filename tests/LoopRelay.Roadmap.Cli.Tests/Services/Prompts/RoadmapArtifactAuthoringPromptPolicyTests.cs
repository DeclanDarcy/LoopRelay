using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Prompts;

public sealed class RoadmapArtifactAuthoringPromptPolicyTests
{
    public static TheoryData<string, string, string, string> PromptCases => new()
    {
        {
            "CreateNewEpic",
            "# CreateNewEpic Implementation-First Guidance",
            "# CreateNewEpic Auxiliary Artifact Limits",
            ".agents/epic.md"
        },
        {
            "RealignEpic",
            "# RealignEpic Implementation-First Guidance",
            "# RealignEpic Auxiliary Artifact Limits",
            ".agents/epic.md"
        },
        {
            "ReimagineEpic",
            "# ReimagineEpic Implementation-First Guidance",
            "# ReimagineEpic Auxiliary Artifact Limits",
            ".agents/epic.md"
        },
        {
            "GenerateMilestoneDeepDivesForEpic",
            "# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance",
            "# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits",
            ".agents/specs/*.md"
        },
        {
            "SplitEpic",
            "# SplitEpic Implementation-First Guidance",
            "# SplitEpic Auxiliary Artifact Limits",
            ".agents/epic"
        },
    };

    [Theory]
    [MemberData(nameof(PromptCases))]
    public void Rendering_carries_template_owned_implementation_first_sections(
        string runtimePromptName,
        string guidanceHeading,
        string limitsHeading,
        string primaryArtifact)
    {
        string rendered = Render(runtimePromptName);

        Assert.Contains(guidanceHeading, rendered, StringComparison.Ordinal);
        Assert.Contains(limitsHeading, rendered, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendering_preserves_prompt_specific_primary_artifact_rules()
    {
        string realign = Render("RealignEpic");
        string reimagine = Render("ReimagineEpic");
        string specs = Render("GenerateMilestoneDeepDivesForEpic");
        string split = Render("SplitEpic");

        Assert.Contains("minimal strategic patch", realign, StringComparison.Ordinal);
        Assert.Contains("epic identity", realign, StringComparison.Ordinal);
        Assert.Contains("Replacing the epic is valid only because the EpicPreparationAudit", reimagine, StringComparison.Ordinal);
        Assert.Contains("Every material redesign decision should trace", reimagine, StringComparison.Ordinal);
        Assert.Contains("One deep-dive spec per epic milestone remains required", specs, StringComparison.Ordinal);
        Assert.Contains("Those files are planning artifacts, but they are not auxiliary output", specs, StringComparison.Ordinal);
        Assert.Contains("Child epic files are primary contracted outputs", split, StringComparison.Ordinal);
        Assert.Contains("Splits are invalid when they merely divide planning phases", split, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_contract_registry_preserves_primary_artifact_boundaries()
    {
        var registry = new PromptContractRegistry(new ProjectionRegistry());

        foreach (string prompt in new[] { "CreateNewEpic", "RealignEpic", "ReimagineEpic" })
        {
            var contract = registry.Get(prompt);

            Assert.Contains(RoadmapArtifactPaths.ActiveEpic, contract.RequiredOutputs);
            Assert.Equal("ArtifactPromotionService", contract.ArtifactWriter);
            Assert.Equal("EpicAuthoringOutputClassifier+EpicArtifactValidator", contract.ParserName);
        }

        var specs = registry.Get("GenerateMilestoneDeepDivesForEpic");
        Assert.Contains(RoadmapArtifactPaths.SpecsDirectory, specs.RequiredOutputs);
        Assert.Equal("SpecBundleWriter", specs.ArtifactWriter);
        Assert.Equal("BundleFileExtractor", specs.ParserName);

        var split = registry.Get("SplitEpic");
        Assert.Contains(RoadmapArtifactPaths.SplitFamiliesDirectory, split.RequiredOutputs);
        Assert.Contains(RoadmapArtifactPaths.ActiveEpic, split.RequiredOutputs);
        Assert.Contains("SplitEpicBundleInterpreter", split.ArtifactWriter, StringComparison.Ordinal);
        Assert.Contains("EpicArtifactValidator", split.ParserName, StringComparison.Ordinal);
    }

    private static string Render(string runtimePromptName) =>
        RoadmapPromptCatalog.RenderRuntime(
            runtimePromptName,
            "project context",
            SecondaryInput(runtimePromptName));

    private static string SecondaryInput(string runtimePromptName) =>
        runtimePromptName switch
        {
            "CreateNewEpic" => "new epic proposal",
            "RealignEpic" => "audit input",
            "ReimagineEpic" => "audit input",
            "SplitEpic" => "split proposal",
            _ => string.Empty,
        };
}
