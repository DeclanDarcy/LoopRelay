using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models;
using LoopRelay.Completion.Primitives;
using LoopRelay.Completion.Services;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models;
using LoopRelay.Projections.Primitives;
using Xunit;

namespace LoopRelay.Completion.Tests.Services;

public sealed class CompletionCertificationServiceTests
{
    [Fact]
    public async Task CloseWorthyCertification_ArchivesSynthesizesAndPassesCompletedEpicToUpdate()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Fully Complete", "None", "Close Epic");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic\n\nSynthesized.");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.CompletedEpicArchiveIndex);
        Assert.Equal(".agents/archive/epics/1.md", result.CompletedEpicSynthesisPath);
        Assert.Equal("# Roadmap Completion Context\n\nUpdated.", await h.ReadAsync(CompletionArtifactPaths.RoadmapCompletionContext));
        Assert.Equal("PLAN", await h.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Equal("DETAILS", await h.ReadAsync(".agents/archive/epics/1/details.md"));
        Assert.Equal("OPCTX", await h.ReadAsync(".agents/archive/epics/1/operational_context.md"));
        Assert.Equal("- [x] milestone", await h.ReadAsync(".agents/archive/epics/1/milestones/m1.md"));
        Assert.Equal("DECISION", await h.ReadAsync(".agents/archive/epics/1/decisions/decisions.md"));
        Assert.Null(await h.ReadAsync(CompletionArtifactPaths.ExecutionPlan));

        CompletionRuntimePromptInvocation update = Assert.Single(
            h.Prompts.Invocations,
            invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext);
        Assert.Equal("# Completed Epic\n\nSynthesized.", update.SecondaryInput);
        Assert.Contains("# Completed Epic", update.ProjectContext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonCloseCertification_ReturnsBlockedAndDoesNotArchiveOrUpdateContext()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        h.Prompts.Handler = invocation =>
            Task.FromResult(invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift
                ? Evaluation("Partially Complete", "None", "Continue Epic")
                : throw new InvalidOperationException(invocation.RuntimePromptName));

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Blocked, result.Outcome);
        Assert.Equal("Continue Epic", result.Decision?.ClosureRecommendation);
        Assert.NotNull(result.BlockerEvidencePath);
        Assert.Null(await h.ReadAsync(".agents/archive/epics/1/plan.md"));
        Assert.Equal("# Roadmap Completion Context\n\nCurrent.", await h.ReadAsync(CompletionArtifactPaths.RoadmapCompletionContext));
        Assert.DoesNotContain(h.Prompts.Invocations, invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic);
        Assert.DoesNotContain(h.Prompts.Invocations, invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext);
    }

    [Fact]
    public async Task ArchiveIndexCountsExistingArchiveDirectories()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        await h.WriteAsync(".agents/archive/epics/1/existing.md", "existing archive");

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Functionally Complete", "Mixed", "Close With Follow-Up");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic 2");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Equal(2, result.CompletedEpicArchiveIndex);
        Assert.Equal("2", Assert.Single(
            h.Prompts.Invocations,
            invocation => invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic).Label);
        Assert.Equal("PLAN", await h.ReadAsync(".agents/archive/epics/2/plan.md"));
    }

    [Fact]
    public async Task CompletionEvaluationAndUpdateContextsIncludeReviewSummariesWhenPresent()
    {
        Harness h = Harness.Create();
        await h.SeedExecutionWorkspaceAsync();
        await h.WriteAsync(OrchestrationArtifactPaths.NonImplementationReview, "# Review\n\nUnresolved docs/report.md.");
        await h.WriteAsync(OrchestrationArtifactPaths.NonImplementationSynthesis, "# Synthesis\n\nUseful extracted context.");
        var promptsWithSummaries = new List<string>();

        h.Prompts.Handler = async invocation =>
        {
            if (invocation.RuntimePromptName is CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift
                or CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                Assert.Contains("Non-Implementation Review Summary", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Unresolved docs/report.md", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Non-Implementation Review Synthesis", invocation.ProjectContext, StringComparison.Ordinal);
                Assert.Contains("Useful extracted context", invocation.ProjectContext, StringComparison.Ordinal);
                promptsWithSummaries.Add(invocation.RuntimePromptName);
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift)
            {
                return Evaluation("Fully Complete", "None", "Close Epic");
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.SynthesizeCompletedEpic)
            {
                await h.WriteAsync($".agents/archive/epics/{invocation.Label}.md", "# Completed Epic\n\nSynthesized.");
                return "synthesized";
            }

            if (invocation.RuntimePromptName == CompletionRuntimePromptNames.UpdateRoadmapCompletionContext)
            {
                return "# Roadmap Completion Context\n\nUpdated.";
            }

            throw new InvalidOperationException(invocation.RuntimePromptName);
        };

        CompletionCertificationResult result = await h.Service.CertifyPlanCompletionAsync(new CompletionCertificationRequest(h.Repository));

        Assert.Equal(CompletionCertificationServiceOutcome.Completed, result.Outcome);
        Assert.Contains(CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift, promptsWithSummaries);
        Assert.Contains(CompletionRuntimePromptNames.UpdateRoadmapCompletionContext, promptsWithSummaries);
    }

    [Fact]
    public async Task AgentCompletionPromptRunner_AppendsImplementationFirstPolicy()
    {
        var runtime = new RecordingAgentRuntime(new AgentTurnResult(
            0,
            AgentTurnState.Completed,
            "ok",
            AgentTokenUsage.Zero));
        var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
        var runner = new AgentCompletionPromptRunner(runtime, repository);

        string output = await runner.RunAsync(new CompletionRuntimePromptInvocation(
            CompletionRuntimePromptNames.EvaluateEpicCompletionAndDrift,
            ProjectContext: "context"));

        Assert.Equal("ok", output);
        string prompt = Assert.Single(runtime.Prompts);
        Assert.Contains("Repository growth is implementation-first", prompt, StringComparison.Ordinal);
        Assert.Contains("The HITL-requested exception is disabled", prompt, StringComparison.Ordinal);
    }

    private static string Evaluation(string completionStatus, string drift, string recommendation) => $$"""
        # Epic Completion and Drift Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-TEST |
        | Epic Name | Test Epic |
        | Overall Completion Status | {{completionStatus}} |
        | Overall Drift Classification | {{drift}} |
        | Evidence Strength | Strong |
        | Closure Recommendation | {{recommendation}} |
        | Primary Reason | Test |
        """;

    private sealed class Harness
    {
        private readonly MemoryArtifactStore store;

        private Harness(MemoryArtifactStore store, Repository repository, FakePromptRunner prompts, CompletionCertificationService service)
        {
            this.store = store;
            Repository = repository;
            Prompts = prompts;
            Service = service;
        }

        public Repository Repository { get; }

        public FakePromptRunner Prompts { get; }

        public CompletionCertificationService Service { get; }

        public static Harness Create()
        {
            var store = new MemoryArtifactStore();
            var repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "/repo" };
            var prompts = new FakePromptRunner();
            var archive = new CompletedEpicArchiveService(store, prompts);
            var service = new CompletionCertificationService(
                store,
                new FakeProjectionService(),
                prompts,
                archive);
            return new Harness(store, repository, prompts, service);
        }

        public async Task SeedExecutionWorkspaceAsync()
        {
            await WriteAsync(CompletionArtifactPaths.ActiveEpic, "# Epic\n\nIntent.");
            await WriteAsync(CompletionArtifactPaths.RoadmapCompletionContext, "# Roadmap Completion Context\n\nCurrent.");
            await WriteAsync(CompletionArtifactPaths.ExecutionPlan, "PLAN");
            await WriteAsync(CompletionArtifactPaths.Details, "DETAILS");
            await WriteAsync(CompletionArtifactPaths.OperationalContext, "OPCTX");
            await WriteAsync(".agents/milestones/m1.md", "- [x] milestone");
            await WriteAsync(".agents/decisions/decisions.md", "DECISION");
            await WriteAsync(".agents/deltas/operational_delta.0001.md", "DELTA");
            await WriteAsync(".agents/handoffs/handoff.md", "HANDOFF");
        }

        public Task WriteAsync(string relativePath, string content) =>
            store.WriteAsync(Resolve(relativePath), content);

        public Task<string?> ReadAsync(string relativePath) =>
            store.ReadAsync(Resolve(relativePath));

        private string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(Repository, relativePath);
    }

    private sealed class FakePromptRunner : ICompletionPromptRunner
    {
        public List<CompletionRuntimePromptInvocation> Invocations { get; } = [];

        public Func<CompletionRuntimePromptInvocation, Task<string>> Handler { get; set; } =
            invocation => throw new InvalidOperationException(invocation.RuntimePromptName);

        public async Task<string> RunAsync(
            CompletionRuntimePromptInvocation invocation,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(invocation);
            return await Handler(invocation);
        }
    }

    private sealed class RecordingAgentRuntime(AgentTurnResult result) : IAgentRuntime
    {
        public List<string> Prompts { get; } = [];

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(result);
        }

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }

    private sealed class FakeProjectionService : IProjectContextProjectionService
    {
        public Task<ProjectContextProjectionResult> EnsureFreshAsync(
            string runtimePromptName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectContextProjectionResult(
                new ProjectionDefinition(
                    runtimePromptName,
                    $"ProjectionFor{runtimePromptName}",
                    $".agents/projections/{runtimePromptName}.md",
                    "# Test Projection",
                    runtimePromptName),
                $"# Projection for {runtimePromptName}",
                Generated: false,
                ProjectionStaleStatus.Fresh,
                []));

        public Task<ProjectionFreshness> EvaluateFreshnessAsync(
            string runtimePromptName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ProjectionFreshness.Fresh);
    }
}
