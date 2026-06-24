using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Services;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionSessionMetricsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    public void TokenEstimatorIsDeterministic(string text, long expectedTokens)
    {
        var estimator = new DeterministicTokenEstimator();

        Assert.Equal(expectedTokens, estimator.EstimateTokenCount(text));
        Assert.Equal(expectedTokens, estimator.EstimateTokenCount(text));
    }

    [Fact]
    public async Task MetricsAreGeneratedFromDecisionReasoningAndOperationalContextEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var artifactStore = new MemoryArtifactStore();
        var reasoningRepository = new FileSystemReasoningRepository(artifactStore, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(artifactStore);
        var service = new DecisionSessionMetricsService(
            harness.RepositoryService,
            harness.Registry,
            decisionRepository,
            reasoningRepository,
            contextStore,
            new DeterministicTokenEstimator());
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, now.AddMinutes(-10)));
        await decisionRepository.SaveCandidateAsync(harness.Repository, CreateCandidate(harness.Repository.Id, now.AddMinutes(-9)));
        await decisionRepository.SaveProposalAsync(harness.Repository, CreateProposal(harness.Repository.Id, now.AddMinutes(-8)));
        ReasoningEvent reasoningEvent = await reasoningRepository.CreateEventAsync(harness.Repository, EventCommand("Cache pressure observed"));
        ReasoningThread reasoningThread = await reasoningRepository.CreateThreadAsync(harness.Repository, new CreateReasoningThreadCommand(
            "Session lifecycle thread",
            ReasoningThreadTheme.DecisionEvolution,
            "Tracks decision session lifecycle evidence.",
            [reasoningEvent.Id],
            ["decision-session"]));
        await reasoningRepository.CreateRelationshipAsync(harness.Repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Supports,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, reasoningThread.Id),
            new ReasoningNarrative("The event supports the lifecycle thread."),
            new ReasoningProvenance("test", "test")));
        await contextStore.SaveAsync(
            harness.Repository,
            new OperationalContextProposal
            {
                ProposalId = "ctx-0001",
                RepositoryId = harness.Repository.Id,
                GeneratedAt = now.AddMinutes(-7),
                Status = OperationalContextProposalStatus.Pending,
                BaselineCurrentContextHash = "baseline",
                GeneratedContentHash = "generated"
            },
            "Operational context revision content.");

        DecisionSessionMetricsSnapshot snapshot = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.Equal(1, snapshot.Metrics.DecisionCount);
        Assert.Equal(1, snapshot.Metrics.DecisionCandidateCount);
        Assert.Equal(1, snapshot.Metrics.DecisionProposalCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningEventCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningThreadCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningRelationshipCount);
        Assert.Equal(1, snapshot.Metrics.OperationalContextRevisionCount);
        Assert.True(snapshot.Metrics.EstimatedTokenCount > 0);
        Assert.True(snapshot.Metrics.ContextByteSize > 0);
        Assert.True(snapshot.Statistics.ActivityRate > 0m);
        Assert.True(snapshot.Cache.EstimatedCacheMissRisk >= 0m);
        Assert.Contains(snapshot.Diagnostics.Sources, source => source.Source == "operational-context-proposals" && source.ItemCount == 1);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Token count", StringComparison.Ordinal));
    }

    private static Decision CreateDecision(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Use decision sessions",
            "Decision sessions carry governance continuity.",
            new DecisionMetadata(repositoryId, timestamp, timestamp),
            null,
            [],
            [],
            [History(timestamp, "Open")]);
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            DecisionCandidateState.Discovered,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Measure session evidence",
            "Metrics should read authoritative evidence.",
            "source",
            [],
            [],
            [],
            [],
            [History(timestamp, DecisionCandidateState.Discovered.ToString())]);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new DecisionProposal(
            "PROP-0001",
            repositoryId,
            "CAND-0001",
            DecisionProposalState.Generated,
            "Add metrics",
            "Metrics are analysis-only.",
            [],
            [],
            null,
            [],
            [],
            [History(timestamp, DecisionProposalState.Generated.ToString())]);
    }

    private static DecisionHistoryEntry History(DateTimeOffset timestamp, string toState)
    {
        return new DecisionHistoryEntry(timestamp, "Created", null, toState, null, []);
    }

    private static CreateReasoningEventCommand EventCommand(string title)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionReframed,
            title,
            new ReasoningNarrative("The session evidence changed."),
            [],
            new ReasoningProvenance("test", "test"),
            [],
            ["decision-session"]);
    }
}
