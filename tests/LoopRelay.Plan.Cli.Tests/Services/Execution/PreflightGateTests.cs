using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Plan.Cli.Services.Execution;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;
using Xunit;

namespace LoopRelay.Plan.Cli.Tests.Services.Execution;

public class PreflightGateTests
{
    private static (PreflightGate Gate, IArtifactStore Store, Repository Repo) New()
    {
        var store = new MemoryArtifactStore();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        return (new PreflightGate(new PlanArtifacts(store, repo)), store, repo);
    }

    private static string Resolve(Repository repo, string rel) => ArtifactPath.ResolveRepositoryPath(repo, rel);

    [Fact]
    public async Task CheckAsync_CleanStateWithEpic_ReturnsNoViolations()
    {
        var (gate, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");

        Assert.Empty(await gate.CheckAsync());
    }

    [Fact]
    public async Task CheckAsync_PlanAlreadyExists_ReportsViolation()
    {
        var (gate, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");

        IReadOnlyList<string> violations = await gate.CheckAsync();

        string violation = Assert.Single(violations);
        Assert.Contains(OrchestrationArtifactPaths.Plan, violation);
        Assert.Contains("already exists", violation);
    }

    [Fact]
    public async Task CheckAsync_OperationalContextAlreadyExists_ReportsViolation()
    {
        var (gate, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.OperationalContext), "CTX");

        IReadOnlyList<string> violations = await gate.CheckAsync();

        string violation = Assert.Single(violations);
        Assert.Contains(OrchestrationArtifactPaths.OperationalContext, violation);
        Assert.Contains("already exists", violation);
    }

    [Fact]
    public async Task CheckAsync_DetailsAlreadyExists_ReportsViolation()
    {
        var (gate, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");

        IReadOnlyList<string> violations = await gate.CheckAsync();

        string violation = Assert.Single(violations);
        Assert.Contains(OrchestrationArtifactPaths.Details, violation);
        Assert.Contains("already exists", violation);
    }

    [Fact]
    public async Task CheckAsync_MilestonesDirectoryNotEmpty_ReportsViolation()
    {
        var (gate, store, repo) = New();
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.SpecsEpic), "EPIC");
        await store.WriteAsync(
            Resolve(repo, ArtifactPath.CombineRelative(OrchestrationArtifactPaths.MilestonesDirectory, "m1.md")),
            "- [ ] a");

        IReadOnlyList<string> violations = await gate.CheckAsync();

        string violation = Assert.Single(violations);
        Assert.Contains(OrchestrationArtifactPaths.MilestonesDirectory, violation);
    }

    [Fact]
    public async Task CheckAsync_EpicMissing_ReportsViolation()
    {
        var (gate, _, _) = New();

        IReadOnlyList<string> violations = await gate.CheckAsync();

        string violation = Assert.Single(violations);
        Assert.Contains(OrchestrationArtifactPaths.SpecsEpic, violation);
        Assert.Contains("not found", violation);
    }

    [Fact]
    public async Task CheckAsync_MultipleViolations_AllReportedTogether()
    {
        var (gate, store, repo) = New();
        // No epic authored; plan and details already exist from a previous (uncleaned) run.
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Plan), "PLAN");
        await store.WriteAsync(Resolve(repo, OrchestrationArtifactPaths.Details), "DETAILS");

        IReadOnlyList<string> violations = await gate.CheckAsync();

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Contains(OrchestrationArtifactPaths.Plan) && v.Contains("already exists"));
        Assert.Contains(violations, v => v.Contains(OrchestrationArtifactPaths.Details) && v.Contains("already exists"));
        Assert.Contains(violations, v => v.Contains(OrchestrationArtifactPaths.SpecsEpic) && v.Contains("not found"));
    }
}
