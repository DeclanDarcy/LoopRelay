using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class FullChainLiveRunnerTests
{
    [Fact]
    public async Task Independent_repeatability_evidence_executes_two_equivalent_clean_runs()
    {
        string repository = Directory.CreateTempSubdirectory("looprelay-full-chain-repeatability-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(repository, "implement.ps1"), """
                Add-Content -LiteralPath (Join-Path $PSScriptRoot 'implementation-runs.txt') -Value 'run'
                $utf8 = [System.Text.UTF8Encoding]::new($false)
                [System.IO.File]::WriteAllText(
                    (Join-Path $PSScriptRoot 'GREETING.md'),
                    "Hello from Loop Relay.`n",
                    $utf8)
                exit 0
                """);
            await File.WriteAllTextAsync(Path.Combine(repository, "verify.ps1"), """
                $path = Join-Path $PSScriptRoot 'GREETING.md'
                if (-not (Test-Path -LiteralPath $path)) { exit 1 }
                $expected = [System.Text.Encoding]::UTF8.GetBytes("Hello from Loop Relay.`n")
                $actual = [System.IO.File]::ReadAllBytes($path)
                if ($actual.Length -ne $expected.Length) { exit 1 }
                for ($i = 0; $i -lt $expected.Length; $i++) {
                    if ($actual[$i] -ne $expected[$i]) { exit 1 }
                }
                exit 0
                """);

            IReadOnlyList<FullChainRepeatabilityRun> runs =
                await FullChainLiveRunner.RecordImplementationRepeatabilityAsync(
                    repository,
                    CancellationToken.None);

            Assert.Equal(2, runs.Count);
            Assert.Equal([1, 2], runs.Select(run => run.Run));
            Assert.All(runs, run =>
            {
                Assert.Equal("GREETING.md absent", run.CleanState);
                Assert.Equal(0, run.ImplementationExitCode);
                Assert.Equal(0, run.VerifierExitCode);
                Assert.Equal(23, run.GreetingByteLength);
                Assert.True(run.Utf8Valid);
                Assert.False(run.Utf8BomPresent);
            });
            Assert.Single(runs.Select(run => run.GreetingSha256).Distinct());
            Assert.Single(runs.Select(run => run.VerifierSha256).Distinct());
            Assert.Equal(
                2,
                (await File.ReadAllLinesAsync(Path.Combine(repository, "implementation-runs.txt"))).Length);
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

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
