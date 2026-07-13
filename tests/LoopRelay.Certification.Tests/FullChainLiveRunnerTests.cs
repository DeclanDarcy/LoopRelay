using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class FullChainLiveRunnerTests
{
    [Fact]
    public void Convergence_accepts_only_the_authorized_execute_time_milestone_evolution()
    {
        Assert.True(FullChainLiveRunner.ConvergedExecuteEntryProducer(
            Product(ProductIdentity.ExecutablePlan, WorkflowIdentity.Plan, "WriteExecutablePlan")));
        Assert.True(FullChainLiveRunner.ConvergedExecuteEntryProducer(
            Product(ProductIdentity.ExecutionMilestoneSet, WorkflowIdentity.Execute, "ExecuteImplementationSlice")));
        Assert.False(FullChainLiveRunner.ConvergedExecuteEntryProducer(
            Product(ProductIdentity.ExecutablePlan, WorkflowIdentity.Execute, "ExecuteImplementationSlice")));
        Assert.False(FullChainLiveRunner.ConvergedExecuteEntryProducer(
            Product(ProductIdentity.ExecutionMilestoneSet, WorkflowIdentity.Execute, "GenerateHandoff")));
    }

    private static ProductRecord Product(
        ProductIdentity identity,
        WorkflowIdentity producer,
        string transition) => new(
            identity,
            producer,
            new WorkflowTransitionIdentity(transition),
            [WorkflowIdentity.Execute],
            "repository-owned certification evidence",
            "canonical",
            [$"{identity}.md"],
            $"causal-{identity}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity}.md"]);
}
