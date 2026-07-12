using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Persistence;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Execution;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Projections;

public sealed class RoadmapPromptContextBuilderTests
{
    [Fact]
    public async Task Selection_context_contains_projection_completion_roadmap_references_and_retired_epics()
    {
        using var repo = new TempRepo();
        repo.Write(RoadmapArtifactPaths.RoadmapCompletionContext, "current strategic state");
        repo.Write(".agents/roadmap/001-roadmap.md", "roadmap 001 body must not be injected");
        repo.Write(".agents/roadmap/b.md", "roadmap b body must not be injected");

        string context = await new RoadmapPromptContextBuilder(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo)).BuildSelectionContextAsync(
            "projection",
            [new RetiredEpic("EPIC-001", "Retired Epic", "Already satisfied.", ".agents/evidence/audits/epic-preparation-audit.0001.md", DateTimeOffset.UtcNow)]);

        Assert.Contains("projection", context, StringComparison.Ordinal);
        Assert.Contains("current strategic state", context, StringComparison.Ordinal);
        Assert.Contains("## Roadmap Source References", context, StringComparison.Ordinal);
        Assert.Contains((string)RoadmapArtifactPaths.RoadmapDirectoryPattern, context, StringComparison.Ordinal);
        Assert.Contains(".agents/roadmap/001-roadmap.md", context, StringComparison.Ordinal);
        Assert.Contains(".agents/roadmap/b.md", context, StringComparison.Ordinal);
        Assert.DoesNotContain("roadmap 001 body must not be injected", context, StringComparison.Ordinal);
        Assert.DoesNotContain("roadmap b body must not be injected", context, StringComparison.Ordinal);
        Assert.Contains("## Retired Epics", context, StringComparison.Ordinal);
        Assert.Contains("EPIC-001", context, StringComparison.Ordinal);
        Assert.Contains("Retired Epic", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_context_rejects_raw_project_context_markers()
    {
        using var repo = new TempRepo();
        var builder = new RoadmapPromptContextBuilder(repo.Artifacts, ExecutionPreparationTestSupport.CreateProvenance(repo));

        Assert.Throws<RoadmapStepException>(() => builder.BuildAuditContext("<!-- BEGIN PROJECT-CONTEXT FILE: 01-purpose.md -->", "epic"));
    }

    [Fact]
    public async Task CompletionEvaluationContextReadsExecutionEvidenceThroughLogicalResolver()
    {
        using var repo = new TempRepo();
        const string evidencePath = ".agents/evidence/execution/execution.0001.md";
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "# Active Epic");
        repo.Write(".agents/specs/s001.md", "# Spec");
        ExecutionPreparationProvenanceService provenance =
            await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/s001.md");
        var resolver = new StubLogicalArtifactResolver(evidencePath, "# SQLite-backed execution evidence");
        var builder = new RoadmapPromptContextBuilder(repo.Artifacts, provenance, resolver);

        string context = await builder.BuildCompletionEvaluationContextAsync("# Projection", evidencePath);

        Assert.Contains("## Execution Evidence: .agents/evidence/execution/execution.0001.md", context, StringComparison.Ordinal);
        Assert.Contains("# SQLite-backed execution evidence", context, StringComparison.Ordinal);
        Assert.False(await repo.Artifacts.ExistsAsync(evidencePath));
    }

    [Fact]
    public async Task CompletionEvaluationContextReadsSqliteExecutionEvidenceAfterExportIsDeleted()
    {
        using var repo = new TempRepo();
        const string evidencePath = ".agents/evidence/execution/execution.0001.md";
        repo.Write(RoadmapArtifactPaths.ActiveEpic, "# Active Epic");
        repo.Write(".agents/specs/s001.md", "# Spec");
        repo.Write(evidencePath, "# SQLite execution evidence");
        ExecutionPreparationProvenanceService provenance =
            await ExecutionPreparationTestSupport.SeedMilestoneSpecsAsync(repo, ".agents/specs/s001.md");
        var sqlite = new WorkspaceSqliteStore();
        await sqlite.ImportAsync(repo.Artifacts);
        await repo.Artifacts.DeleteAsync(evidencePath);
        var sqliteArtifacts = new RoadmapArtifacts(
            repo.Store,
            repo.Repository,
            new SqliteExecutionEvidenceStore(repo.Repository));
        var builder = new RoadmapPromptContextBuilder(sqliteArtifacts, provenance);

        string context = await builder.BuildCompletionEvaluationContextAsync("# Projection", evidencePath);

        Assert.Contains("# SQLite execution evidence", context, StringComparison.Ordinal);
        Assert.False(await repo.Artifacts.ExistsAsync(evidencePath));
    }

    [Fact]
    public async Task Retained_runtime_prompt_does_not_embed_a_resolved_policy_profile()
    {
        using var repo = new TempRepo();
        var runtime = new ScriptedAgentRuntime(ScriptedAgentRuntime.Completed("ok"));
        var runner = new RoadmapPromptRunner(runtime, repo.Repository, new TestConsole());

        await runner.RunRuntimePromptAsync("SelectNextEpic", "project context", string.Empty, CancellationToken.None);

        string prompt = Assert.Single(runtime.Prompts);
        Assert.DoesNotContain("## Implementation-First Prompt Policy", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Repository growth is implementation-first", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("## Resolved Prompt Policy", prompt, StringComparison.Ordinal);
    }

    private sealed class StubLogicalArtifactResolver(string path, string content) : ILogicalArtifactResolver
    {
        public Task<LogicalArtifactResolutionResult> ResolveAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(relativePath, path, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(LogicalArtifactResolutionResult.Unresolved(
                    new LogicalArtifactDescriptor(relativePath, LogicalArtifactDomain.Unknown, LogicalArtifactStorageKind.Unknown),
                    LogicalArtifactResolutionStatus.WrongDomain,
                    "Unexpected logical artifact path."));
            }

            return Task.FromResult(LogicalArtifactResolutionResult.Resolved(
                new LogicalArtifactDescriptor(
                    relativePath,
                    LogicalArtifactDomain.ExecutionEvidence,
                    LogicalArtifactStorageKind.SqliteCanonicalRecord,
                    "execution:0001"),
                content));
        }
    }
}
