using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class EvalRoadmapMilestonePromptContextTests
{
    [Fact]
    public void Build_blocks_when_active_epic_is_missing()
    {
        string repo = CreateRepo();

        EvalRoadmapMilestonePromptContextResult result =
            EvalRoadmapMilestonePromptContext.Build(repo);

        Assert.False(result.IsUsable);
        Assert.Empty(result.Sections);
        Assert.Contains(".agents/epic.md", result.Evidence);
        Assert.Contains("missing", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_blocks_when_active_epic_is_empty()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/epic.md", "   ");

        EvalRoadmapMilestonePromptContextResult result =
            EvalRoadmapMilestonePromptContext.Build(repo);

        Assert.False(result.IsUsable);
        Assert.Contains("empty", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_blocks_when_active_epic_is_malformed()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/epic.md", "# Implementation Spec\n\nNo active-epic shape.");

        EvalRoadmapMilestonePromptContextResult result =
            EvalRoadmapMilestonePromptContext.Build(repo);

        Assert.False(result.IsUsable);
        Assert.Contains("malformed", result.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing top-level `# Epic:` heading", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_blocks_when_active_epic_is_ambiguous()
    {
        string repo = CreateRepo();
        Write(
            repo,
            ".agents/epic.md",
            ValidEpic() + "\n\n# Epic: Conflicting Epic\n");

        EvalRoadmapMilestonePromptContextResult result =
            EvalRoadmapMilestonePromptContext.Build(repo);

        Assert.False(result.IsUsable);
        Assert.Contains("multiple top-level `# Epic:` headings", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_returns_active_epic_prompt_section_and_metadata_for_valid_epic()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/epic.md", ValidEpic());

        EvalRoadmapMilestonePromptContextResult result =
            EvalRoadmapMilestonePromptContext.Build(repo);

        Assert.True(result.IsUsable);
        Assert.Equal("Active Epic", Assert.Single(result.Sections).Title);
        Assert.Equal(".agents/epic.md", result.Sections.Single().SourcePath);
        Assert.Contains("# Epic: Eval Readiness", result.Sections.Single().Content, StringComparison.Ordinal);
        Assert.Equal(".agents/epic.md", result.Metadata["context.active_epic.path"]);
        Assert.Equal("Active Epic", result.Metadata["context.active_epic.section"]);
        Assert.Equal("valid", result.Metadata["context.active_epic.status"]);
        Assert.Matches("^[a-f0-9]{64}$", result.Metadata["context.active_epic.hash"]);
    }

    private static string CreateRepo() =>
        Directory.CreateTempSubdirectory("looprelay-eval-context-").FullName;

    private static void Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string ValidEpic() =>
        """
        # Epic: Eval Readiness

        ## Epic Metadata

        | Field | Value |
        | --- | --- |
        | Epic ID | EVAL-1 |
        | Status | Planned / Not Implemented / Not Verified |
        | Output Path | .agents/epic.md |

        ## Strategic Purpose

        Preserve eval-derived implementation scope.

        ## Desired Capability

        The system can plan a bounded eval-derived implementation slice.

        ## Acceptance Criteria

        - Milestone deep dives preserve the selected epic.

        ## Milestone Roadmap

        | Milestone ID | Milestone Name | Purpose | Outcome | Depends On | Completion Signal |
        | --- | --- | --- | --- | --- | --- |
        | M1 | Prepare Context | Establish the context contract. | Context is usable. | None | Context validation passes. |
        """;
}
