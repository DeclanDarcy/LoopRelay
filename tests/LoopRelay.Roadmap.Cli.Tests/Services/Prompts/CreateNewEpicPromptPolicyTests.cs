using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Prompts;

public sealed class CreateNewEpicPromptPolicyTests
{
    [Fact]
    public void CreateNewEpic_sections_are_selected_from_auxiliary_artifact_policy()
    {
        CreateNewEpicPromptSectionSet strict =
            CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(false);
        CreateNewEpicPromptSectionSet allowed =
            CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(true);

        Assert.Contains("# CreateNewEpic Implementation-First Guidance", strict.EpicImplementationFirstGuidance);
        Assert.Contains("# CreateNewEpic Auxiliary Artifact Limits", strict.EpicAuxiliaryArtifactLimits);
        Assert.Contains("CreateNewEpicImplementationFirstGuidance", strict.ActiveSectionSourceHashes.Keys);
        Assert.Contains("CreateNewEpicAuxiliaryArtifactLimits", strict.ActiveSectionSourceHashes.Keys);
        Assert.Equal(string.Empty, allowed.EpicImplementationFirstGuidance);
        Assert.Equal(string.Empty, allowed.EpicAuxiliaryArtifactLimits);
        Assert.Empty(allowed.ActiveSectionSourceHashes);
    }

    [Fact]
    public void CreateNewEpic_rendering_injects_strict_sections_only_when_auxiliary_files_are_disabled()
    {
        string strict = RoadmapPromptCatalog.RenderRuntime(
            "CreateNewEpic",
            "project context",
            "proposal",
            Policy(allowAuxiliaryNonImplementationFiles: false));
        string allowed = RoadmapPromptCatalog.RenderRuntime(
            "CreateNewEpic",
            "project context",
            "proposal",
            Policy(allowAuxiliaryNonImplementationFiles: true));

        Assert.Contains("# CreateNewEpic Implementation-First Guidance", strict, StringComparison.Ordinal);
        Assert.Contains("# CreateNewEpic Auxiliary Artifact Limits", strict, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", strict, StringComparison.Ordinal);
        Assert.DoesNotContain("# Invalid Content", strict, StringComparison.Ordinal);
        Assert.DoesNotContain("{epicImplementationFirstGuidance}", strict, StringComparison.Ordinal);

        Assert.DoesNotContain("# CreateNewEpic Implementation-First Guidance", allowed, StringComparison.Ordinal);
        Assert.DoesNotContain("# CreateNewEpic Auxiliary Artifact Limits", allowed, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", allowed, StringComparison.Ordinal);
        Assert.DoesNotContain("{epicAuxiliaryArtifactLimits}", allowed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_runner_skips_legacy_composer_for_CreateNewEpic_only()
    {
        using var repo = new TempRepo();
        var runtime = new ScriptedAgentRuntime(
            ScriptedAgentRuntime.Completed("created"),
            ScriptedAgentRuntime.Completed("selected"));
        var runner = new RoadmapPromptRunner(
            runtime,
            repo.Repository,
            new TestConsole(),
            Policy(allowAuxiliaryNonImplementationFiles: false));

        await runner.RunRuntimePromptAsync("CreateNewEpic", "project context", "proposal", CancellationToken.None);
        await runner.RunRuntimePromptAsync("SelectNextEpic", "project context", string.Empty, CancellationToken.None);

        Assert.DoesNotContain(
            ImplementationFirstPromptPolicyComposer.SectionHeading,
            runtime.Prompts[0],
            StringComparison.Ordinal);
        Assert.Contains("# CreateNewEpic Auxiliary Artifact Limits", runtime.Prompts[0], StringComparison.Ordinal);
        Assert.Contains(
            ImplementationFirstPromptPolicyComposer.SectionHeading,
            runtime.Prompts[1],
            StringComparison.Ordinal);
    }

    [Fact]
    public void CreateNewEpic_section_body_is_not_hard_coded_in_csharp_files()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "src");
        string[] sectionLines =
        [
            .. Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.Text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            .. Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.Text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
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

    private static RoadmapRuntimePromptPolicy Policy(bool allowAuxiliaryNonImplementationFiles) =>
        RoadmapRuntimePromptPolicy.FromArtifactPolicy(
            new NonImplementationArtifactPolicyOptions(
                AllowHitlRequestedNonImplementationFiles: false,
                AllowAuxiliaryNonImplementationFiles: allowAuxiliaryNonImplementationFiles));

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
}
