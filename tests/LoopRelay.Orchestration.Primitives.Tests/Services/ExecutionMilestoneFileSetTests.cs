using LoopRelay.Orchestration.Services;

namespace LoopRelay.Orchestration.Primitives.Tests.Services;

public sealed class ExecutionMilestoneFileSetTests
{
    [Fact]
    public void Evaluate_accepts_unique_numeric_milestone_identities()
    {
        ExecutionMilestoneFileSetResult result = ExecutionMilestoneFileSet.Evaluate(
            [".agents/milestones/m1.md", ".agents/milestones/m2-local-verification.md"]);

        Assert.True(result.IsValid, result.Explanation);
    }

    [Fact]
    public void Evaluate_rejects_duplicate_identity_across_different_labels()
    {
        ExecutionMilestoneFileSetResult result = ExecutionMilestoneFileSet.Evaluate(
            [".agents/milestones/m1-implementation.md", ".agents/milestones/m01-verification.md"]);

        Assert.False(result.IsValid);
        Assert.Equal(["M1"], result.DuplicateIdentities);
    }

    [Fact]
    public void Evaluate_rejects_non_numeric_or_empty_labels()
    {
        ExecutionMilestoneFileSetResult result = ExecutionMilestoneFileSet.Evaluate(
            [".agents/milestones/m-one.md", ".agents/milestones/m2-.md"]);

        Assert.False(result.IsValid);
        Assert.Equal(["m-one.md", "m2-.md"], result.InvalidFiles);
    }
}
