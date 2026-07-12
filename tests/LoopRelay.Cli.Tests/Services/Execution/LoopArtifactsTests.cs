using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Execution;

public sealed class LoopArtifactsTests
{
    [Fact]
    public async Task PersistDecisions_binds_advisory_recommendation_to_canonical_decision_fact()
    {
        var store = new MemoryArtifactStore();
        Repository repository = Repository();
        var history = new RecordingHistoryStore();
        var recommendations = new RecordingRecommendationStore();
        var artifacts = new LoopArtifacts(store, repository, history, recommendations);
        CanonicalCausalContext causality = NewAttempt();
        AgentSessionIdentity session = AgentSessionIdentity.New();
        TurnIdentity turn = TurnIdentity.New();

        (LoopHistoryRecord decision, ExecutionRecommendationEvidence recommendation) =
            await artifacts.PersistDecisionsAsync(
                "# Decisions\n\nImplement the slice.",
                TestAgentConfiguration.Execution,
                causality,
                session,
                turn,
                "Decision agent recommendation.");

        Assert.Equal(decision.Identity.Value, recommendation.DecisionProduct.Value);
        Assert.Equal(causality, decision.Causality);
        Assert.Equal(session, recommendation.SourceSession);
        Assert.Equal(turn, recommendation.SourceTurn);
        Assert.Same(recommendation, Assert.Single(recommendations.Items));
        Assert.Equal(
            "# Decisions\n\nImplement the slice.",
            await artifacts.ReadAsync(OrchestrationArtifactPaths.Decisions));
    }

    [Fact]
    public async Task RotateLiveHandoff_appends_canonical_history_before_deleting_projection()
    {
        var store = new MemoryArtifactStore();
        Repository repository = Repository();
        var history = new RecordingHistoryStore();
        var artifacts = new LoopArtifacts(store, repository, history, new RecordingRecommendationStore());
        CanonicalCausalContext causality = NewAttempt();
        await artifacts.WriteAsync(OrchestrationArtifactPaths.LiveHandoff, "handoff");

        string? rotated = await artifacts.RotateLiveHandoffAsync(causality);

        Assert.Equal("handoff", rotated);
        LoopHistoryAppendRequest request = Assert.Single(history.Appends);
        Assert.Equal(LoopHistoryKind.Handoff, request.Kind);
        Assert.Equal(causality, request.Causality);
        Assert.False(await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff));
    }

    [Fact]
    public async Task RotateLiveHandoff_keeps_projection_when_authoritative_append_fails()
    {
        var store = new MemoryArtifactStore();
        Repository repository = Repository();
        var artifacts = new LoopArtifacts(
            store,
            repository,
            new ThrowingHistoryStore(),
            new RecordingRecommendationStore());
        await artifacts.WriteAsync(OrchestrationArtifactPaths.LiveHandoff, "handoff");

        await Assert.ThrowsAsync<IOException>(() => artifacts.RotateLiveHandoffAsync(NewAttempt()));

        Assert.Equal("handoff", await artifacts.ReadAsync(OrchestrationArtifactPaths.LiveHandoff));
    }

    [Fact]
    public async Task ReadLatestDecisions_prefers_live_compatibility_projection_without_mutating_history()
    {
        var store = new MemoryArtifactStore();
        Repository repository = Repository();
        var history = new RecordingHistoryStore { Latest = Fact(LoopHistoryKind.Decisions, "history") };
        var artifacts = new LoopArtifacts(store, repository, history, new RecordingRecommendationStore());
        await artifacts.WriteAsync(OrchestrationArtifactPaths.Decisions, "live");

        (string? content, string? path) = await artifacts.ReadLatestDecisionsAsync();

        Assert.Equal("live", content);
        Assert.Equal(OrchestrationArtifactPaths.Decisions, path);
        Assert.Equal(0, history.ReadCount);
    }

    private static Repository Repository() => new()
    {
        Id = Guid.NewGuid(),
        Name = "repo",
        Path = "/repo",
    };

    private static CanonicalCausalContext NewAttempt() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    private static LoopHistoryRecord Fact(LoopHistoryKind kind, string content) => new(
        HistoryFactIdentity.New(),
        kind,
        1,
        DateTimeOffset.UtcNow,
        content,
        LoopHistoryRecord.ComputeContentHash(content),
        NewAttempt(),
        HistoryEvidenceAttachments.Empty,
        materializedRelativePath: ".agents/history.md");

    private sealed class RecordingHistoryStore : ILoopHistoryStore
    {
        public List<LoopHistoryAppendRequest> Appends { get; } = [];
        public LoopHistoryRecord? Latest { get; init; }
        public int ReadCount { get; private set; }

        public Task<LoopHistoryRecord> AppendAsync(
            LoopHistoryAppendRequest request,
            CancellationToken cancellationToken = default)
        {
            Appends.Add(request);
            return Task.FromResult(new LoopHistoryRecord(
                HistoryFactIdentity.New(),
                request.Kind,
                Appends.Count,
                DateTimeOffset.UtcNow,
                request.Content,
                LoopHistoryRecord.ComputeContentHash(request.Content),
                request.Causality,
                request.Evidence,
                request.Supersedes,
                $".agents/history/{Appends.Count:0000}.md"));
        }

        public Task<LoopHistoryRecord?> ReadLatestAsync(
            LoopHistoryKind kind,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(Latest);
        }
    }

    private sealed class ThrowingHistoryStore : ILoopHistoryStore
    {
        public Task<LoopHistoryRecord> AppendAsync(
            LoopHistoryAppendRequest request,
            CancellationToken cancellationToken = default) =>
            throw new IOException("history unavailable");

        public Task<LoopHistoryRecord?> ReadLatestAsync(
            LoopHistoryKind kind,
            CancellationToken cancellationToken = default) => Task.FromResult<LoopHistoryRecord?>(null);
    }

    private sealed class RecordingRecommendationStore : IExecutionRecommendationEvidenceStore
    {
        public List<ExecutionRecommendationEvidence> Items { get; } = [];

        public Task AppendAsync(
            ExecutionRecommendationEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<ExecutionRecommendationEvidence?> ReadAsync(
            ExecutionRecommendationIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(item => item.Identity == identity));

        public Task<ExecutionRecommendationEvidence?> ReadForDecisionAsync(
            DecisionProductVersionIdentity decisionProduct,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.LastOrDefault(item => item.DecisionProduct == decisionProduct));
    }
}
