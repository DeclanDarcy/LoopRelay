using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Prompts;

public sealed class RoadmapArtifactAuthoringPromptPolicyTests
{
    public static TheoryData<string, string, string, string, string, string, string, string> PromptCases => new()
    {
        {
            "CreateNewEpic",
            "# CreateNewEpic Implementation-First Guidance",
            "# CreateNewEpic Auxiliary Artifact Limits",
            "{epicImplementationFirstGuidance}",
            "{epicAuxiliaryArtifactLimits}",
            ".agents/epic.md",
            "CreateNewEpicImplementationFirstGuidance",
            "CreateNewEpicAuxiliaryArtifactLimits"
        },
        {
            "RealignEpic",
            "# RealignEpic Implementation-First Guidance",
            "# RealignEpic Auxiliary Artifact Limits",
            "{realignEpicImplementationFirstGuidance}",
            "{realignEpicAuxiliaryArtifactLimits}",
            ".agents/epic.md",
            "RealignEpicImplementationFirstGuidance",
            "RealignEpicAuxiliaryArtifactLimits"
        },
        {
            "ReimagineEpic",
            "# ReimagineEpic Implementation-First Guidance",
            "# ReimagineEpic Auxiliary Artifact Limits",
            "{reimagineEpicImplementationFirstGuidance}",
            "{reimagineEpicAuxiliaryArtifactLimits}",
            ".agents/epic.md",
            "ReimagineEpicImplementationFirstGuidance",
            "ReimagineEpicAuxiliaryArtifactLimits"
        },
        {
            "GenerateMilestoneDeepDivesForEpic",
            "# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance",
            "# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits",
            "{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}",
            "{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}",
            ".agents/specs/*.md",
            "GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance",
            "GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits"
        },
        {
            "SplitEpic",
            "# SplitEpic Implementation-First Guidance",
            "# SplitEpic Auxiliary Artifact Limits",
            "{splitEpicImplementationFirstGuidance}",
            "{splitEpicAuxiliaryArtifactLimits}",
            ".agents/epic",
            "SplitEpicImplementationFirstGuidance",
            "SplitEpicAuxiliaryArtifactLimits"
        },
    };

    [Theory]
    [MemberData(nameof(PromptCases))]
    public void Prompt_owned_sections_are_selected_from_auxiliary_artifact_policy(
        string runtimePromptName,
        string guidanceHeading,
        string limitsHeading,
        string guidancePlaceholder,
        string limitsPlaceholder,
        string primaryArtifact,
        string guidanceSectionKey,
        string limitsSectionKey)
    {
        SelectedSections strict = SelectSections(runtimePromptName, allowAuxiliaryNonImplementationFiles: false);
        SelectedSections allowed = SelectSections(runtimePromptName, allowAuxiliaryNonImplementationFiles: true);

        Assert.Contains(guidanceHeading, strict.ImplementationFirstGuidance, StringComparison.Ordinal);
        Assert.Contains(limitsHeading, strict.AuxiliaryArtifactLimits, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, strict.ImplementationFirstGuidance + strict.AuxiliaryArtifactLimits, StringComparison.Ordinal);
        Assert.DoesNotContain(guidancePlaceholder, strict.ImplementationFirstGuidance, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsPlaceholder, strict.AuxiliaryArtifactLimits, StringComparison.Ordinal);
        Assert.Contains(guidanceSectionKey, strict.ActiveSectionSourceHashes.Keys);
        Assert.Contains(limitsSectionKey, strict.ActiveSectionSourceHashes.Keys);
        Assert.Equal(string.Empty, allowed.ImplementationFirstGuidance);
        Assert.Equal(string.Empty, allowed.AuxiliaryArtifactLimits);
        Assert.Empty(allowed.ActiveSectionSourceHashes);
    }

    [Theory]
    [MemberData(nameof(PromptCases))]
    public void Prompt_owned_rendering_injects_strict_sections_and_removes_placeholders(
        string runtimePromptName,
        string guidanceHeading,
        string limitsHeading,
        string guidancePlaceholder,
        string limitsPlaceholder,
        string primaryArtifact,
        string guidanceSectionKey,
        string limitsSectionKey)
    {
        string strict = Render(runtimePromptName, allowAuxiliaryNonImplementationFiles: false);
        string allowed = Render(runtimePromptName, allowAuxiliaryNonImplementationFiles: true);

        Assert.Contains(guidanceHeading, strict, StringComparison.Ordinal);
        Assert.Contains(limitsHeading, strict, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, strict, StringComparison.Ordinal);
        Assert.DoesNotContain("# Invalid Content", strict, StringComparison.Ordinal);
        Assert.DoesNotContain(guidancePlaceholder, strict, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsPlaceholder, strict, StringComparison.Ordinal);

        Assert.DoesNotContain(guidanceHeading, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsHeading, allowed, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(guidancePlaceholder, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsPlaceholder, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(guidanceSectionKey, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsSectionKey, allowed, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_owned_rendering_preserves_prompt_specific_primary_artifact_rules()
    {
        string realign = Render("RealignEpic", allowAuxiliaryNonImplementationFiles: false);
        string reimagine = Render("ReimagineEpic", allowAuxiliaryNonImplementationFiles: false);
        string specs = Render("GenerateMilestoneDeepDivesForEpic", allowAuxiliaryNonImplementationFiles: false);
        string split = Render("SplitEpic", allowAuxiliaryNonImplementationFiles: false);

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
    public async Task Runtime_runner_skips_legacy_composer_for_all_prompt_owned_artifact_authoring_prompts()
    {
        using var repo = new TempRepo();
        var runtime = new ScriptedAgentRuntime();
        var runner = new RoadmapPromptRunner(
            runtime,
            repo.Repository,
            new TestConsole(),
            Policy(allowAuxiliaryNonImplementationFiles: false));

        foreach (string prompt in PromptOwnedRuntimePrompts)
        {
            await runner.RunRuntimePromptAsync(prompt, "project context", SecondaryInput(prompt), CancellationToken.None);
        }

        await runner.RunRuntimePromptAsync("SelectNextEpic", "project context", string.Empty, CancellationToken.None);

        foreach (string prompt in runtime.Prompts.Take(PromptOwnedRuntimePrompts.Length))
        {
            Assert.DoesNotContain(
                ImplementationFirstPromptPolicyComposer.SectionHeading,
                prompt,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            ImplementationFirstPromptPolicyComposer.SectionHeading,
            runtime.Prompts.Last(),
            StringComparison.Ordinal);
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

    [Fact]
    public void Prompt_owned_section_body_text_is_not_hard_coded_in_csharp_files()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "src");
        string[] sectionLines =
        [
            .. PromptOwnedSectionTexts()
                .SelectMany(text => text.Split(
                    Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        ];

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(file);
            string[] segments = fullPath.Split(Path.DirectorySeparatorChar);
            if (segments.Contains("bin") || segments.Contains("obj"))
            {
                continue;
            }

            string content = File.ReadAllText(fullPath);
            foreach (string sectionLine in sectionLines.Where(line => line.Length > 30))
            {
                Assert.DoesNotContain(sectionLine, content, StringComparison.Ordinal);
            }
        }
    }

    private static readonly string[] PromptOwnedRuntimePrompts =
    [
        "CreateNewEpic",
        "RealignEpic",
        "ReimagineEpic",
        "GenerateMilestoneDeepDivesForEpic",
        "SplitEpic",
    ];

    private static string Render(string runtimePromptName, bool allowAuxiliaryNonImplementationFiles) =>
        RoadmapPromptCatalog.RenderRuntime(
            runtimePromptName,
            "project context",
            SecondaryInput(runtimePromptName),
            Policy(allowAuxiliaryNonImplementationFiles));

    private static SelectedSections SelectSections(
        string runtimePromptName,
        bool allowAuxiliaryNonImplementationFiles) =>
        runtimePromptName switch
        {
            "CreateNewEpic" => From(CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(
                allowAuxiliaryNonImplementationFiles)),
            "RealignEpic" => From(RealignEpicPromptSections.ForAuxiliaryArtifactPolicy(
                allowAuxiliaryNonImplementationFiles)),
            "ReimagineEpic" => From(ReimagineEpicPromptSections.ForAuxiliaryArtifactPolicy(
                allowAuxiliaryNonImplementationFiles)),
            "GenerateMilestoneDeepDivesForEpic" => From(
                GenerateMilestoneDeepDivesForEpicPromptSections.ForAuxiliaryArtifactPolicy(
                    allowAuxiliaryNonImplementationFiles)),
            "SplitEpic" => From(SplitEpicPromptSections.ForAuxiliaryArtifactPolicy(
                allowAuxiliaryNonImplementationFiles)),
            _ => throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, null),
        };

    private static SelectedSections From(CreateNewEpicPromptSectionSet sections) =>
        new(
            sections.EpicImplementationFirstGuidance,
            sections.EpicAuxiliaryArtifactLimits,
            sections.ActiveSectionSourceHashes);

    private static SelectedSections From(RealignEpicPromptSectionSet sections) =>
        new(
            sections.RealignEpicImplementationFirstGuidance,
            sections.RealignEpicAuxiliaryArtifactLimits,
            sections.ActiveSectionSourceHashes);

    private static SelectedSections From(ReimagineEpicPromptSectionSet sections) =>
        new(
            sections.ReimagineEpicImplementationFirstGuidance,
            sections.ReimagineEpicAuxiliaryArtifactLimits,
            sections.ActiveSectionSourceHashes);

    private static SelectedSections From(GenerateMilestoneDeepDivesForEpicPromptSectionSet sections) =>
        new(
            sections.GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance,
            sections.GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits,
            sections.ActiveSectionSourceHashes);

    private static SelectedSections From(SplitEpicPromptSectionSet sections) =>
        new(
            sections.SplitEpicImplementationFirstGuidance,
            sections.SplitEpicAuxiliaryArtifactLimits,
            sections.ActiveSectionSourceHashes);

    private static string SecondaryInput(string runtimePromptName) =>
        runtimePromptName switch
        {
            "CreateNewEpic" => "new epic proposal",
            "RealignEpic" => "audit input",
            "ReimagineEpic" => "audit input",
            "SplitEpic" => "split proposal",
            _ => string.Empty,
        };

    private static RoadmapRuntimePromptPolicy Policy(bool allowAuxiliaryNonImplementationFiles) =>
        RoadmapRuntimePromptPolicy.FromArtifactPolicy(
            new NonImplementationArtifactPolicyOptions(
                AllowHitlRequestedNonImplementationFiles: false,
                AllowAuxiliaryNonImplementationFiles: allowAuxiliaryNonImplementationFiles));

    private static IEnumerable<string> PromptOwnedSectionTexts()
    {
        yield return Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.Text;
        yield return Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.Text;
        yield return Core.Prompts.NonImplementation.RealignEpicImplementationFirstGuidance.Text;
        yield return Core.Prompts.NonImplementation.RealignEpicAuxiliaryArtifactLimits.Text;
        yield return Core.Prompts.NonImplementation.ReimagineEpicImplementationFirstGuidance.Text;
        yield return Core.Prompts.NonImplementation.ReimagineEpicAuxiliaryArtifactLimits.Text;
        yield return Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.Text;
        yield return Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.Text;
        yield return Core.Prompts.NonImplementation.SplitEpicImplementationFirstGuidance.Text;
        yield return Core.Prompts.NonImplementation.SplitEpicAuxiliaryArtifactLimits.Text;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record SelectedSections(
        string ImplementationFirstGuidance,
        string AuxiliaryArtifactLimits,
        IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);
}
