using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class ExecutionMilestoneGateTests
{
    [Fact]
    public void Evaluate_reports_readiness_when_any_trackable_milestone_checkbox_exists()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/milestones/m1.md", "# M1\n\n- [ ] Implement capability.");

        ExecutionMilestoneGateResult result = ExecutionMilestoneGate.Evaluate(
            repo,
            [".agents/milestones/m1.md"]);

        Assert.True(result.ReadinessSatisfied);
        Assert.False(result.CompletionSatisfied);
        Assert.Equal(ProductValidationState.Unknown, result.MilestoneSetValidationState);
        Assert.Equal(1, result.TotalCheckboxes);
        Assert.Equal(0, result.CompletedCheckboxes);
        Assert.Equal(["- [ ] Implement capability."], result.UntickedItems);
        Assert.Equal([".agents/milestones/m1.md"], result.Evidence);
    }

    [Fact]
    public void Evaluate_reports_completion_when_every_trackable_milestone_checkbox_is_checked()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/milestones/m1.md", "# M1\n\n- [x] Implement capability.\n- [X] Verify it.");

        ExecutionMilestoneGateResult result = ExecutionMilestoneGate.Evaluate(
            repo,
            [".agents/milestones/m1.md"]);

        Assert.True(result.ReadinessSatisfied);
        Assert.True(result.CompletionSatisfied);
        Assert.Equal(ProductValidationState.Valid, result.MilestoneSetValidationState);
        Assert.Equal(2, result.TotalCheckboxes);
        Assert.Equal(2, result.CompletedCheckboxes);
        Assert.Empty(result.UntickedItems);
    }

    [Fact]
    public void Evaluate_rejects_milestone_files_without_trackable_checkboxes()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/milestones/m1.md", "# M1\n\nNo task list.");

        ExecutionMilestoneGateResult result = ExecutionMilestoneGate.Evaluate(
            repo,
            [".agents/milestones/m1.md"]);

        Assert.False(result.ReadinessSatisfied);
        Assert.False(result.CompletionSatisfied);
        Assert.Equal(ProductValidationState.Invalid, result.MilestoneSetValidationState);
    }

    [Fact]
    public void CountCheckboxes_ignores_checkbox_shapes_inside_code_fences()
    {
        (int total, int completed, IReadOnlyList<string> unticked) = ExecutionMilestoneGate.CountCheckboxes(
            """
            ```text
            - [ ] ignored
            - [x] ignored too
            ```
            - [ ] open
            - [x] closed
            """);

        Assert.Equal(2, total);
        Assert.Equal(1, completed);
        Assert.Equal(["- [ ] open"], unticked);
    }

    private static string CreateRepo() =>
        Directory.CreateTempSubdirectory("looprelay-execution-milestone-gate-").FullName;

    private static void Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
